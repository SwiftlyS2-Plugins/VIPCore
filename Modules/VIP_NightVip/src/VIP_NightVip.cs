using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Players;
using VIPCore.Contract;
using System.Collections.Concurrent;
using System;

namespace VIP_NightVip;

public class VIP_NightVipConfig
{
    public string VIPGroup { get; set; } = "VIP";
    public string PluginStartTime { get; set; } = "20:00:00";
    public string PluginEndTime { get; set; } = "08:00:00";
    public string Timezone { get; set; } = "UTC";
    public float CheckTimer { get; set; } = 10.0f;
    public string Tag { get; set; } = "[NightVIP]";
}

public class VipCoreConfigSnapshot
{
    public int TimeMode { get; set; } = 0;
}

[PluginMetadata(Id = "VIP_NightVip", Version = "1.0.0", Name = "VIP_NightVip", Author = "aga", Description = "Gives VIP between a certain period of time.")]
public partial class VIP_NightVip : BasePlugin {
  private IVipCoreApiV1? _vipApi;
  private VIP_NightVipConfig _config = new();

  private readonly ConcurrentDictionary<ulong, bool> _grantedByUs = new();

  private CancellationTokenSource? _checkTimerCts;

  private TimeZoneInfo _timeZoneInfo = TimeZoneInfo.Utc;
  private TimeSpan _startTime;
  private TimeSpan _endTime;
  private bool _timeConfigValid = true;

  public VIP_NightVip(ISwiftlyCore core) : base(core)
  {
  }

  public override void ConfigureSharedInterface(IInterfaceManager interfaceManager) {
  }

  public override void UseSharedInterface(IInterfaceManager interfaceManager) {
    _vipApi = null;

    if (interfaceManager.HasSharedInterface("VIPCore.Api.v1"))
      _vipApi = interfaceManager.GetSharedInterface<IVipCoreApiV1>("VIPCore.Api.v1");

    RegisterWhenReady();
  }

  public override void Load(bool hotReload) {
    Core.Configuration
      .InitializeJsonWithModel<VIP_NightVipConfig>("config.jsonc", "NightVip")
      .Configure(builder =>
      {
        var configPath = Core.Configuration.GetConfigPath("config.jsonc");
        builder.AddJsonFile(configPath, optional: false, reloadOnChange: true);
      });

    _config = Core.Configuration.Manager.GetSection("NightVip").Get<VIP_NightVipConfig>() ?? new VIP_NightVipConfig();

    ParseTimeConfig();

    Core.Event.OnClientPutInServer += OnClientPutInServer;
    Core.Event.OnClientDisconnected += OnClientDisconnected;

    RegisterWhenReady();
  }

  private void ParseTimeConfig()
  {
    try
    {
      if (_config.Timezone.StartsWith("UTC+") || _config.Timezone.StartsWith("UTC-"))
      {
          var sign = _config.Timezone[3] == '+' ? 1 : -1;
          var offsetStr = _config.Timezone.Substring(4); // 02:00
          if (TimeSpan.TryParse(offsetStr, out var offset))
          {
              _timeZoneInfo = TimeZoneInfo.CreateCustomTimeZone(
                  "CustomTimezone", offset * sign, "CustomTimezone", "CustomTimezone"
              );
          }
          else
          {
              _timeZoneInfo = TimeZoneInfo.Utc;
          }
      }
      else
      {
          _timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(_config.Timezone);
      }
    }
    catch (Exception)
    {
        _timeZoneInfo = TimeZoneInfo.Utc;
    }
    
    try
    {
        _startTime = TimeSpan.Parse(_config.PluginStartTime);
        _endTime = TimeSpan.Parse(_config.PluginEndTime);
        _timeConfigValid = true;
    }
    catch (Exception)
    {
        _timeConfigValid = false;
    }
  }

  public override void Unload() {
    _checkTimerCts?.Cancel();
    _checkTimerCts = null;

    Core.Event.OnClientPutInServer -= OnClientPutInServer;
    Core.Event.OnClientDisconnected -= OnClientDisconnected;

    if (_vipApi != null)
    {
      _vipApi.OnCoreReady -= OnCoreReady;
    }

    _grantedByUs.Clear();
  }

  private void RegisterWhenReady()
  {
    if (_vipApi == null) return;

    if (_vipApi.IsCoreReady())
      OnCoreReady();
    else
      _vipApi.OnCoreReady += OnCoreReady;
  }

  private void OnCoreReady()
  {
    _checkTimerCts?.Cancel();
    
    var interval = _config.CheckTimer > 0 ? _config.CheckTimer : 10.0f;
    _checkTimerCts = Core.Scheduler.RepeatBySeconds(interval, () => CheckAllPlayers());
  }

  private void OnClientPutInServer(IOnClientPutInServerEvent @event)
  {
    var player = Core.PlayerManager.GetPlayer(@event.PlayerId);
    if (player == null || player.IsFakeClient) return;

    Core.Scheduler.DelayBySeconds(1.0f, () => CheckPlayer(player));
  }

  private void OnClientDisconnected(IOnClientDisconnectedEvent @event)
  {
    var player = Core.PlayerManager.GetPlayer(@event.PlayerId);
    if (player == null || player.IsFakeClient) return;

    if (_grantedByUs.TryRemove(player.SteamID, out _))
    {
      if (_vipApi != null && _vipApi.IsClientVip(player))
        _vipApi.RemoveClientVip(player);
    }
  }

  private void CheckAllPlayers()
  {
    if (_vipApi == null || !_vipApi.IsCoreReady()) return;

    // Reload config so Timezone and times apply dynamically
    var updatedConfig = Core.Configuration.Manager.GetSection("NightVip").Get<VIP_NightVipConfig>();
    if (updatedConfig != null)
    {
        _config = updatedConfig;
        ParseTimeConfig();
    }

    for (var i = 0; i < Core.PlayerManager.PlayerCap; i++)
    {
      var player = Core.PlayerManager.GetPlayer(i);
      if (player == null || player.IsFakeClient) continue;
      if (!player.IsValid) continue;

      CheckPlayer(player);
    }
  }

  private void CheckPlayer(IPlayer player)
  {
    if (_vipApi == null || !_vipApi.IsCoreReady()) return;
    if (!player.IsValid || player.IsFakeClient) return;
    if (!_timeConfigValid) return;

    var currentTimeInTimeZone = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _timeZoneInfo);
    var now = currentTimeInTimeZone.TimeOfDay;

    bool isVipTime = _startTime < _endTime
        ? now >= _startTime && now < _endTime
        : now >= _startTime || now < _endTime;

    if (isVipTime)
    {
      if (!_vipApi.IsClientVip(player))
      {
        if (!_grantedByUs.ContainsKey(player.SteamID))
        {
          // We mark it temporarily so we don't spam GiveClientVip while it's processing
          _grantedByUs[player.SteamID] = false;

          var timeUnitsToEnd = GetGrantTimeUnitsUntilEndOfWindow(currentTimeInTimeZone);
          if (timeUnitsToEnd <= 0)
          {
              _grantedByUs.TryRemove(player.SteamID, out _);
              return;
          }

          _vipApi.GiveClientVip(player, _config.VIPGroup, timeUnitsToEnd);
          
          Core.Scheduler.DelayBySeconds(1.5f, () => {
              if (player == null || !player.IsValid) return;

              if (_vipApi.IsClientVip(player))
              {
                  _grantedByUs[player.SteamID] = true;
                  var localizer = Core.Translation.GetPlayerLocalizer(player);
                  player.SendMessage(MessageType.Chat, localizer["nightvip.Granted", _config.Tag]);
              }
              else
              {
                  // It failed to grant VIP. Clear the flag so it retries next check.
                  _grantedByUs.TryRemove(player.SteamID, out _);
                  Core.Logger.LogWarning($"[VIP_NightVip] Failed to verify VIP for {player.Controller?.PlayerName} ({player.SteamID}) after calling GiveClientVip. Is VIPGroup '{_config.VIPGroup}' valid in VIPCore?");
              }
          });
        }
      }
      else if (_grantedByUs.TryGetValue(player.SteamID, out var granted) && !granted)
      {
        // Player has VIP, but we didn't fully mark them as granted yet
        _grantedByUs[player.SteamID] = true;
      }
    }
    else
    {
      if (_grantedByUs.TryGetValue(player.SteamID, out var wasGrantedByUs) && wasGrantedByUs)
      {
        if (_vipApi.IsClientVip(player))
          _vipApi.RemoveClientVip(player);

        _grantedByUs.TryRemove(player.SteamID, out _);
      }
    }
  }

  private int GetGrantTimeUnitsUntilEndOfWindow(DateTime currentTimeInTimeZone)
  {
    var nowTod = currentTimeInTimeZone.TimeOfDay;

    TimeSpan remaining;
    if (_startTime < _endTime)
    {
      remaining = _endTime - nowTod;
    }
    else
    {
      remaining = nowTod < _endTime
        ? _endTime - nowTod
        : (TimeSpan.FromDays(1) - nowTod) + _endTime;
    }

    var remainingSeconds = (int)Math.Ceiling(Math.Max(0, remaining.TotalSeconds));
    if (remainingSeconds <= 0) return 0;

    var timeMode = Core.Configuration.Manager.GetSection("vip").Get<VipCoreConfigSnapshot>()?.TimeMode ?? 0;
    return timeMode switch
    {
      1 => Math.Max(1, (int)Math.Ceiling(remainingSeconds / 60.0)),
      2 => Math.Max(1, (int)Math.Ceiling(remainingSeconds / 3600.0)),
      3 => Math.Max(1, (int)Math.Ceiling(remainingSeconds / 86400.0)),
      _ => Math.Max(1, remainingSeconds)
    };
  }
}