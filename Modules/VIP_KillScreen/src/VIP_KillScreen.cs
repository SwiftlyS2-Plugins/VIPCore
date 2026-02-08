using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.SchemaDefinitions;
using VIPCore.Contract;

namespace VIP_KillScreen;

[PluginMetadata(Id = "VIP_KillScreen", Version = "1.0.0", Name = "[VIP] KillScreen", Author = "aga", Description = "Applies a health shot screen effect on kill for VIP players.")]
public partial class VIP_KillScreen : BasePlugin
{
    private const string FeatureKey = "vip.killscreen";
    private const float DefaultEffectDuration = 1.0f;

    private IVipCoreApiV1? _vipApi;
    private bool _isFeatureRegistered;

    public VIP_KillScreen(ISwiftlyCore core) : base(core)
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
        Core.GameEvent.HookPost<EventPlayerDeath>(OnPlayerDeath);
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

        _vipApi.RegisterFeature(FeatureKey, FeatureType.Toggle, null,
            displayNameResolver: p => Core.Translation.GetPlayerLocalizer(p)["vip.killscreen"]);

        _isFeatureRegistered = true;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event)
    {
        if (_vipApi == null) return HookResult.Continue;

        var attacker = @event.Accessor.GetPlayer("attacker");
        if (attacker == null || !attacker.IsValid || attacker.IsFakeClient) return HookResult.Continue;

        // Ignore suicide
        var victim = @event.Accessor.GetPlayer("userid");
        if (victim != null && attacker.Slot == victim.Slot) return HookResult.Continue;

        if (!_vipApi.IsClientVip(attacker)) return HookResult.Continue;
        if (_vipApi.GetPlayerFeatureState(attacker, FeatureKey) != FeatureState.Enabled) return HookResult.Continue;

        var controller = attacker.Controller as CCSPlayerController;
        if (controller == null || !controller.IsValid) return HookResult.Continue;

        var pawn = controller.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid) return HookResult.Continue;

        var duration = DefaultEffectDuration;
        try
        {
            var config = _vipApi.GetFeatureValue<KillScreenConfig>(attacker, FeatureKey);
            if (config != null && config.Duration > 0)
                duration = config.Duration;
        }
        catch
        {
            // Use default duration if config parsing fails
        }

        var currentTime = Core.Engine.GlobalVars.CurrentTime;
        pawn.HealthShotBoostExpirationTime.Value = currentTime + duration;
        pawn.HealthShotBoostExpirationTimeUpdated();

        return HookResult.Continue;
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

public class KillScreenConfig
{
    public float Duration { get; set; } = 1.0f;
}