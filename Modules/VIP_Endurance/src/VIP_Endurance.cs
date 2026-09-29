using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameHooks;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using VIPCore.Contract;

namespace VIP_Endurance;

[PluginMetadata(
    Id = "VIP_Endurance",
    Version = "1.0.0",
    Name = "[VIP] Endurance",
    Author = "R1KO / SwiftlyS2 port",
    Description = "Prevents weapon hits from slowing VIP players.")]
public class VIP_Endurance : BasePlugin
{
    private const string FeatureKey = "vip.endurance";

    private IVipCoreApiV1? _vipApi;
    private bool _isFeatureRegistered;

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
        Core.GameHooks.Pawn.PostThink.Pre += OnPlayerPawnPostThink;
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
            null,
            displayNameResolver: player =>
                Core.Translation.GetPlayerLocalizer(player)[FeatureKey]);

        _isFeatureRegistered = true;
    }

    private void OnPlayerPawnPostThink(ref PostThinkPawnPreContext context)
    {
        if (_vipApi == null) return;

        var player = context.Params.Player;
        if (player == null || !player.IsValid || player.IsFakeClient || !player.IsAlive) return;
        if (!_vipApi.IsClientVip(player)) return;
        if (_vipApi.GetPlayerFeatureState(player, FeatureKey) != FeatureState.Enabled) return;

        var pawn = player.PlayerPawn;
        if (pawn is not { IsValid: true }) return;
        if (pawn.VelocityModifier >= 1.0f) return;

        pawn.VelocityModifier = 1.0f;
        pawn.VelocityModifierUpdated();
    }

    public override void Unload()
    {
        Core.GameHooks.Pawn.PostThink.Pre -= OnPlayerPawnPostThink;

        if (_vipApi == null) return;

        _vipApi.OnCoreReady -= RegisterVipFeature;

        if (_isFeatureRegistered)
            _vipApi.UnregisterFeature(FeatureKey);
    }
}