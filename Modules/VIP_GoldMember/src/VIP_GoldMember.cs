using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Players;
using VIPCore.Contract;
using System.Collections.Concurrent;

namespace VIP_GoldMember;

public class GoldMemberConfig
{
    public string Dns { get; set; } = "example.com";
    public string VipGroup { get; set; } = "VIP";
    public int Duration { get; set; } = 0;
    public float CheckIntervalSeconds { get; set; } = 10.0f;
}

[PluginMetadata(Id = "VIP_GoldMember", Version = "1.0.0", Name = "VIP_GoldMember", Author = "aga", Description = "Grants VIP to players who have a specified DNS/tag in their name.")]
public partial class VIP_GoldMember : BasePlugin {
  private IVipCoreApiV1? _vipApi;
  private GoldMemberConfig _config = new();

  private readonly ConcurrentDictionary<ulong, bool> _grantedByUs = new();

  private CancellationTokenSource? _checkTimerCts;

  public VIP_GoldMember(ISwiftlyCore core) : base(core)
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
      .InitializeJsonWithModel<GoldMemberConfig>("config.jsonc", "GoldMember")
      .Configure(builder =>
      {
        var configPath = Core.Configuration.GetConfigPath("config.jsonc");
        builder.AddJsonFile(configPath, optional: false, reloadOnChange: true);
      });

    _config = Core.Configuration.Manager.GetSection("GoldMember").Get<GoldMemberConfig>() ?? new GoldMemberConfig();

    Core.Event.OnClientPutInServer += OnClientPutInServer;
    Core.Event.OnClientDisconnected += OnClientDisconnected;

    RegisterWhenReady();
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
    _checkTimerCts = Core.Scheduler.RepeatBySeconds(_config.CheckIntervalSeconds, () => CheckAllPlayers());
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
      if (_config.Duration == 0 && _vipApi != null && _vipApi.IsClientVip(player))
        _vipApi.RemoveClientVip(player);
    }
  }

  private void CheckAllPlayers()
  {
    if (_vipApi == null || !_vipApi.IsCoreReady()) return;

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

    var playerName = player.Controller?.PlayerName;
    if (string.IsNullOrEmpty(playerName)) return;

    var hasDns = playerName.Contains(_config.Dns, StringComparison.OrdinalIgnoreCase);

    if (hasDns)
    {
      if (!_vipApi.IsClientVip(player))
      {
        _vipApi.GiveClientTemporaryVip(player, _config.VipGroup, _config.Duration);
        _grantedByUs[player.SteamID] = true;
      }
      else if (!_grantedByUs.ContainsKey(player.SteamID))
        _grantedByUs[player.SteamID] = false;
    }
    else
    {
      if (_config.Duration == 0 && _grantedByUs.TryGetValue(player.SteamID, out var wasGrantedByUs) && wasGrantedByUs)
      {
        if (_vipApi.IsClientVip(player))
          _vipApi.RemoveClientVip(player);

        _grantedByUs.TryRemove(player.SteamID, out _);
      }
    }
  }
}