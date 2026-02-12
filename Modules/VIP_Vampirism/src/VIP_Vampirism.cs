using System;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.SchemaDefinitions;
using VIPCore.Contract;

namespace VIP_Vampirism;

[PluginMetadata(Id = "VIP_Vampirism", Version = "1.0.0", Name = "VIP_Vampirism", Author = "aga", Description = "No description.")]
public partial class VIP_Vampirism : BasePlugin
{
    private const string FeatureKey = "vip.vampirism";

    private IVipCoreApiV1? _vipApi;
    private bool _isFeatureRegistered;

    public VIP_Vampirism(ISwiftlyCore core) : base(core)
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
        Core.GameEvent.HookPost<EventPlayerHurt>(OnPlayerHurt);
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
            displayNameResolver: p => Core.Translation.GetPlayerLocalizer(p)["vip.vampirism"]
        );

        _isFeatureRegistered = true;
    }

    private HookResult OnPlayerHurt(EventPlayerHurt @event)
    {
        if (_vipApi == null) return HookResult.Continue;

        var attackerId = @event.Attacker;
        if (attackerId <= 0) return HookResult.Continue;

        var victimId = @event.UserId;
        if (victimId == attackerId) return HookResult.Continue;

        var attacker = Core.PlayerManager.GetPlayer(attackerId);
        if (attacker == null || attacker.IsFakeClient || !attacker.IsValid) return HookResult.Continue;

        if (!_vipApi.IsClientVip(attacker)) return HookResult.Continue;
        if (_vipApi.GetPlayerFeatureState(attacker, FeatureKey) != FeatureState.Enabled) return HookResult.Continue;

        var dmgHealth = @event.DmgHealth;
        if (dmgHealth <= 0) return HookResult.Continue;

        float percent;
        try
        {
            var config = _vipApi.GetFeatureValue<VampirismConfig>(attacker, FeatureKey);
            percent = config?.Percent ?? 0.0f;
        }
        catch
        {
            percent = 0.0f;
        }

        if (percent <= 0.0f) return HookResult.Continue;

        var controller = attacker.Controller as CCSPlayerController;
        if (controller == null || !controller.IsValid) return HookResult.Continue;

        var pawn = controller.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid) return HookResult.Continue;

        var heal = (int)MathF.Round(dmgHealth * (percent / 100.0f));
        if (heal <= 0) return HookResult.Continue;

        var newHealth = pawn.Health + heal;
        if (newHealth > pawn.MaxHealth)
            newHealth = pawn.MaxHealth;

        pawn.Health = newHealth;
        pawn.HealthUpdated();

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

public class VampirismConfig
{
    public float Percent { get; set; } = 0.0f;
}