using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using VIPCore.Contract;

namespace VIP_Endurance;

[PluginMetadata(
    Id = "VIP_Endurance",
    Version = "1.1.0",
    Name = "[VIP] Endurance",
    Author = "Pisex / SwiftlyS2 port",
    Description = "Prevents weapon hits from slowing VIP players.")]
public class VIP_Endurance : BasePlugin
{
    // The original Pisex module uses "Endurance". The old SwiftlyS2 port
    // incorrectly used "vip.endurance", so that key remains as a compatibility
    // alias for existing configs.
    private const string FeatureKey = "Endurance";
    private const string CompatibilityFeatureKey = "vip.endurance";

    private IVipCoreApiV1? _vipApi;
    private bool _isFeatureRegistered;
    private bool _isCompatibilityFeatureRegistered;

    private readonly bool[] _enabled = new bool[65];
    private readonly List<IPlayer> _cachedPlayers = new(64);
    private bool _playerListDirty = true;

    public VIP_Endurance(ISwiftlyCore core) : base(core)
    {
    }

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
    {
    }

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        _vipApi = null;
        _isFeatureRegistered = false;

        try
        {
            if (interfaceManager.HasSharedInterface("VIPCore.Api.v1"))
                _vipApi = interfaceManager.GetSharedInterface<IVipCoreApiV1>("VIPCore.Api.v1");

            RegisterVipFeatureWhenReady();
        }
        catch (Exception exception)
        {
            Core.Logger.LogWarning(
                "[VIP_Endurance] VIPCore API is not available: {Message}",
                exception.Message);
        }
    }

    public override void Load(bool hotReload)
    {
        // The original module runs from GameFrame. OnTick is the SwiftlyS2
        // equivalent and runs late enough to undo the slowdown applied by a hit.
        Core.Event.OnTick += OnTick;
        Core.Event.OnClientConnected += OnClientConnected;
        Core.Event.OnClientDisconnected += OnClientDisconnected;

        RegisterVipFeatureWhenReady();
    }

    private void RegisterVipFeatureWhenReady()
    {
        if (_vipApi == null) return;

        if (_vipApi.IsCoreReady())
            RegisterVipFeature();
        else
            _vipApi.OnCoreReady += RegisterVipFeature;
    }

    private void RegisterVipFeature()
    {
        if (_vipApi == null || _isFeatureRegistered) return;

        _vipApi.RegisterFeature(
            FeatureKey,
            FeatureType.Toggle,
            OnFeatureToggled,
            displayNameResolver: player =>
                Core.Translation.GetPlayerLocalizer(player)["vip.endurance"]);

        _isFeatureRegistered = true;

        _vipApi.RegisterFeature(
            CompatibilityFeatureKey,
            FeatureType.Toggle,
            OnFeatureToggled,
            displayNameResolver: player =>
                Core.Translation.GetPlayerLocalizer(player)["vip.endurance"]);

        _isCompatibilityFeatureRegistered = true;

        _vipApi.PlayerLoaded += OnPlayerLoaded;
        _vipApi.PlayerRemoved += OnPlayerRemoved;

        // Required for hot-loading while VIP players are already online.
        Core.Scheduler.NextTick(RefreshAllPlayerStates);
    }

    private void OnFeatureToggled(IPlayer player, FeatureState state)
    {
        Core.Scheduler.NextTick(() =>
        {
            if (player.PlayerID < 0 || player.PlayerID >= _enabled.Length) return;
            _enabled[player.PlayerID] = GetEffectiveFeatureState(player);
        });
    }

    private void OnPlayerLoaded(IPlayer player, string group)
    {
        if (player.PlayerID < 0 || player.PlayerID >= _enabled.Length) return;
        _enabled[player.PlayerID] = GetEffectiveFeatureState(player);
        _playerListDirty = true;
    }

    private void OnPlayerRemoved(IPlayer player, string group)
    {
        if (player.PlayerID < 0 || player.PlayerID >= _enabled.Length) return;
        _enabled[player.PlayerID] = false;
    }

    private void OnClientConnected(IOnClientConnectedEvent @event)
    {
        if (@event.PlayerId >= 0 && @event.PlayerId < _enabled.Length)
            _enabled[@event.PlayerId] = false;

        _playerListDirty = true;
    }

    private void OnClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        if (@event.PlayerId >= 0 && @event.PlayerId < _enabled.Length)
            _enabled[@event.PlayerId] = false;

        _playerListDirty = true;
    }

    private bool GetEffectiveFeatureState(IPlayer player)
    {
        if (_vipApi == null || !_vipApi.IsClientVip(player)) return false;

        // Prefer the original key. Fall back to the key used by v1.0.0.
        var state = _vipApi.GetPlayerFeatureState(player, FeatureKey);
        if (state != FeatureState.NoAccess)
            return state == FeatureState.Enabled;

        return _vipApi.GetPlayerFeatureState(
            player,
            CompatibilityFeatureKey) == FeatureState.Enabled;
    }

    private void RefreshAllPlayerStates()
    {
        if (_vipApi == null) return;

        _cachedPlayers.Clear();
        _cachedPlayers.AddRange(Core.PlayerManager.GetAllPlayers());
        _playerListDirty = false;

        foreach (var player in _cachedPlayers)
        {
            if (!player.IsValid || player.IsFakeClient) continue;
            if (player.PlayerID < 0 || player.PlayerID >= _enabled.Length) continue;

            _enabled[player.PlayerID] = GetEffectiveFeatureState(player);
        }
    }

    private void OnTick()
    {
        if (_playerListDirty)
        {
            _cachedPlayers.Clear();
            _cachedPlayers.AddRange(Core.PlayerManager.GetAllPlayers());
            _playerListDirty = false;
        }

        for (var index = 0; index < _cachedPlayers.Count; index++)
        {
            var player = _cachedPlayers[index];
            if (!player.IsValid || player.IsFakeClient || !player.IsAlive) continue;

            var playerId = player.PlayerID;
            if (playerId < 0 || playerId >= _enabled.Length) continue;
            if (!_enabled[playerId]) continue;

            var pawn = player.PlayerPawn;
            if (pawn is not { IsValid: true }) continue;
            if (pawn.VelocityModifier >= 1.0f) continue;

            pawn.VelocityModifier = 1.0f;
            pawn.VelocityModifierUpdated();
        }
    }

    public override void Unload()
    {
        Core.Event.OnTick -= OnTick;
        Core.Event.OnClientConnected -= OnClientConnected;
        Core.Event.OnClientDisconnected -= OnClientDisconnected;

        if (_vipApi == null) return;

        _vipApi.OnCoreReady -= RegisterVipFeature;
        _vipApi.PlayerLoaded -= OnPlayerLoaded;
        _vipApi.PlayerRemoved -= OnPlayerRemoved;

        if (_isFeatureRegistered)
            _vipApi.UnregisterFeature(FeatureKey);

        if (_isCompatibilityFeatureRegistered)
            _vipApi.UnregisterFeature(CompatibilityFeatureKey);
    }
}