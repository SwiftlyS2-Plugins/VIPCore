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
    Version = "1.0", 
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

        _isFeatureRegistered = true;
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
        if (player == null || !player.IsValid || player.IsFakeClient) return;
        if (_grantedByUs.TryGetValue(player.SteamID, out var permissions) && permissions.Length > 0)
        {
            if (_vipApi != null && _vipApi.IsClientVip(player))
                TakeFlags(player);
        }

        _grantedByUs.Remove(player.SteamID);
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
            TakeFlags(player);
        }
    }
    private void GiveFlags(IPlayer player)
    {
        if (_vipApi == null || !player.IsValid || player.IsFakeClient) return;

        var config = _vipApi.GetFeatureValue<FlagsConfig>(player, FeatureKey);
        if (config == null) return;

        var steamId = player.SteamID;
        var permissions = config.Permissions.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var grantedPermissions = new List<string>();
        foreach (var permission in permissions)
        {
            if (!Core.Permission.PlayerHasPermission(player.SteamID, permission))
            {
                Core.Permission.AddPermission(steamId, permission);
            }
            grantedPermissions.Add(permission);
        }
        _grantedByUs[steamId] = grantedPermissions.ToArray(); // Track what we granted for cleanup on disconnect
    }
    private void TakeFlags(IPlayer player)
    {
        if (_vipApi == null || !player.IsValid || player.IsFakeClient) return;

        var steamId = player.SteamID;
        foreach (var permission in _grantedByUs.TryGetValue(steamId, out var perms) ? perms : Array.Empty<string>())
        {
            if (Core.Permission.PlayerHasPermission(player.SteamID, permission))
            {
                Core.Permission.RemovePermission(steamId, permission);
            }
        }
        _grantedByUs.Remove(steamId);
    }
    
    
    public override void Unload()
    {
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