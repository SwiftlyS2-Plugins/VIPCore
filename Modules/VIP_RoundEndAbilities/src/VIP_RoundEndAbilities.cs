using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using VIPCore.Contract;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace VIP_RoundEndAbilities;

[PluginMetadata(Id = "VIP_RoundEndAbilities", Version = "1.0.0", Name = "VIP_RoundEndAbilities", Author = "shmitz", Description = "Gives items when player spawns")]
public partial class VIP_RoundEndAbilities : BasePlugin
{
    private const string FeatureKey = "vip.round_end_abilities";

    private IVipCoreApiV1? _vipApi;
    private bool _isFeatureRegistered;
    private List<IPlayer> _playersWithAppliedAbilities = new();

    public VIP_RoundEndAbilities(ISwiftlyCore core) : base(core)
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

            RegisterVipFeaturesWhenReady();
        }
        catch
        {
        }
    }

    public override void Load(bool hotReload)
    {
        RegisterVipFeaturesWhenReady();
    }


    private void RegisterVipFeaturesWhenReady()
    {
        if (_vipApi == null) return;

        if (_vipApi.IsCoreReady())
            RegisterVipFeatures();
        else
            _vipApi.OnCoreReady += RegisterVipFeatures;
    }

    private void RegisterVipFeatures()
    {
        if (_vipApi == null || _isFeatureRegistered) return;

        _vipApi.RegisterFeature(
            FeatureKey,
            FeatureType.Toggle,
            null,
            displayNameResolver: p => Core.Translation.GetPlayerLocalizer(p)["vip.round_end_abilities"]
        );

        _isFeatureRegistered = true;
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnEventRoundEnd(EventRoundEnd @event)
    {
        if (_vipApi == null) return HookResult.Continue;
        var players = Core.PlayerManager.GetAlive();
        foreach (var player in players)
        {
            if (player == null || player.PlayerPawn == null) return HookResult.Continue;

            if (!_vipApi.IsClientVip(player)) return HookResult.Continue;
            if (_vipApi.GetPlayerFeatureState(player, FeatureKey) != FeatureState.Enabled) return HookResult.Continue;

            var config = _vipApi.GetFeatureValue<Config>(player, FeatureKey);
            if (config == null) return HookResult.Continue;
            Core.Scheduler.NextTick(() => ApplyAbilities(player, config));
        }

        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnEventRoundPrestart(EventRoundPrestart @event)
    {
        var players = _playersWithAppliedAbilities.ToList();

        foreach (var player in players)
        {
            if (player == null || player.PlayerPawn == null)
            {
                _playersWithAppliedAbilities.Remove(player);
                continue;
            }
            RemoveAbilities(player);
        }

        return HookResult.Continue;
    }

    private void ApplyAbilities(IPlayer player, Config config)
    {
        if (!player.IsValid) return;    
        var pawn = player.PlayerPawn;
        if (pawn == null) return;
        if (pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE) return;

        pawn.VelocityModifier = config.SpeedModifier;
        pawn.VelocityModifierUpdated();

        pawn.ActualGravityScale = config.GravityModifier;
        pawn.GravityScaleUpdated();

        if (!_playersWithAppliedAbilities.Contains(player))
            _playersWithAppliedAbilities.Add(player);
    }

    private void RemoveAbilities(IPlayer player)
    {
        _playersWithAppliedAbilities.Remove(player);
        Core.Scheduler.NextTick(() =>
        {
            if (!player.IsValid) return;
            var pawn = player.PlayerPawn;
            if (pawn == null) return;
            if (pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE) return;

            pawn.VelocityModifier = 1f;
            pawn.VelocityModifierUpdated();
            pawn.ActualGravityScale = 1f;
            pawn.GravityScaleUpdated();
        });

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
}

public class Config
{
    public float SpeedModifier = 2f;
    public float GravityModifier = 0.5f;
}