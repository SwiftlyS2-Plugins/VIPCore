using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.Translation;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Helpers;
using SwiftlyS2.Shared.ProtobufDefinitions;
using VIPCore.Contract;

namespace VIP_Flags;

#pragma warning disable CS9107

[PluginMetadata
(
    Id = "VIP_Flags", 
    Version = "1.0.2", 
    Name = "VIP_Flags", 
    Author = "SLAYER", 
    Description = "Manages VIP flags for players"
)]
public partial class VIP_Flags(ISwiftlyCore core) : BasePlugin(core)
{
    public static new ISwiftlyCore Core { get; private set; } = null!;
    private ILocalizer Localizer => core.Localizer;
    private const string FeatureKey = "vip.flags";
    private IVipCoreApiV1? _vipApi;
    private bool _isFeatureRegistered;
    private CancellationTokenSource? _checkTimerCts;
    private readonly Dictionary<ulong, string[]> _grantedByUs = new();
    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        _vipApi = null;
        _isFeatureRegistered = false;

        try
        {
            if (interfaceManager.HasSharedInterface("VIPCore.Api.v1"))
                _vipApi = interfaceManager.GetSharedInterface<IVipCoreApiV1>("VIPCore.Api.v1");

            RegisterWhenReady();
        }
        catch (Exception ex)
        {
            Core.Logger.LogWarning("[VIP_YourFeature] Failed to get VIPCore API: {Message}", ex.Message);
        }
    }
    public override void Load(bool hotReload) 
    {
        // Ensure static Core is initialized before any usage.
        // Swiftly injects the instance core via the primary constructor parameter `core`.
        Core = core;
        RegisterWhenReady();
        Core.Event.OnClientPutInServer += OnClientPutInServer;
        Core.Event.OnClientDisconnected += OnClientDisconnected;
    }
    private void RegisterWhenReady()
    {
        if (_vipApi == null) return;

        if (_vipApi.IsCoreReady())
            RegisterVipFeatures();
        else
            _vipApi.OnCoreReady += RegisterVipFeatures;
    }
     private void RegisterVipFeatures()
    {
        _checkTimerCts?.Cancel();
        _checkTimerCts = Core.Scheduler.RepeatBySeconds(10f, () => CheckAllPlayers());
        if (_vipApi == null || _isFeatureRegistered) return;

        _vipApi.RegisterFeature(FeatureKey, FeatureType.Toggle, null, displayNameResolver: p => Core.Translation.GetPlayerLocalizer(p)["vip.flags"]);
        _vipApi.DescribeFeature(FeatureKey, Describe);

        _isFeatureRegistered = true;
    }

    private string? Describe(IPlayer player)
    {
        try
        {
            var config = _vipApi?.GetFeatureValue<FlagsConfig>(player, FeatureKey);
            var count = config == null ? 0 : SplitPermissions(config.Permissions).Length;
            if (count == 0) return null;

            var localizer = Core.Translation.GetPlayerLocalizer(player);
            return count == 1
                ? localizer["vip.flags.description.one"]
                : localizer["vip.flags.description", count.ToString(CultureInfo.InvariantCulture)];
        }
        catch
        {
            return null;
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
    private void OnClientPutInServer(IOnClientPutInServerEvent @event)
    {
        var player = Core.PlayerManager.GetPlayer(@event.PlayerId);
        if (player == null || !player.IsValid || player.IsFakeClient) return;
        var playerId = @event.PlayerId;
        Core.Scheduler.DelayBySeconds(1.0f, () =>
        {
            var current = Core.PlayerManager.GetPlayer(playerId);
            if (current != null) CheckPlayer(current);
        });
    }
    private void OnClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        var player = Core.PlayerManager.GetPlayer(@event.PlayerId);
        if (player == null || player.IsFakeClient) return;

        TakeFlags(player.SteamID);
    }
    private void CheckPlayer(IPlayer player)
    {
        if (_vipApi == null || !player.IsValid || player.IsFakeClient) return;
        if (_vipApi.IsClientVip(player) && _vipApi.GetPlayerFeatureState(player, FeatureKey) == FeatureState.Enabled)
        {
            GiveFlags(player);
            return;
        }
        if(!_vipApi.IsClientVip(player) || _vipApi.GetClientVipGroups(player).Count() == 0 || _vipApi.GetPlayerFeatureState(player, FeatureKey) != FeatureState.Enabled)
        {
            TakeFlags(player.SteamID);
        }
    }
    private void GiveFlags(IPlayer player)
    {
        if (_vipApi == null || !player.IsValid || player.IsFakeClient) return;

        var config = _vipApi.GetFeatureValue<FlagsConfig>(player, FeatureKey);
        if (config == null) return;

        var steamId = player.SteamID;
        var permissions = SplitPermissions(config.Permissions);
        var granted = _grantedByUs.TryGetValue(steamId, out var held) ? held.ToHashSet() : new HashSet<string>();

        // Only permissions Flags added are tracked, so taking them back never strips one the player has from elsewhere.
        foreach (var permission in permissions)
        {
            if (granted.Contains(permission) || Core.Permission.PlayerHasPermission(steamId, permission)) continue;

            Core.Permission.AddPermission(steamId, permission);
            granted.Add(permission);
        }

        foreach (var dropped in granted.Except(permissions).ToList())
        {
            Core.Permission.RemovePermission(steamId, dropped);
            granted.Remove(dropped);
        }

        _grantedByUs[steamId] = granted.ToArray();
    }
    private void TakeFlags(ulong steamId)
    {
        if (!_grantedByUs.Remove(steamId, out var permissions)) return;

        foreach (var permission in permissions)
        {
            if (Core.Permission.PlayerHasPermission(steamId, permission))
            {
                Core.Permission.RemovePermission(steamId, permission);
            }
        }
    }

    private static string[] SplitPermissions(string permissions) =>
        permissions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToArray();
    
    
    public override void Unload()
    {
        foreach (var steamId in _grantedByUs.Keys.ToList())
            TakeFlags(steamId);

        if (_vipApi != null)
        {
            _vipApi.OnCoreReady -= RegisterVipFeatures;

            if (_isFeatureRegistered)
                _vipApi.UnregisterFeature(FeatureKey);
        }
    }
    public class FlagsConfig
    {
        public string Permissions { get; set; } = "Permissions,Flags";
    }
} 