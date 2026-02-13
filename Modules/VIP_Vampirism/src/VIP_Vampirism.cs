using System;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.SchemaDefinitions;
using VIPCore.Contract;
using SwiftlyS2.Shared.GameEvents;

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

    [GameEventHandler(HookMode.Post)]
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


        var config = _vipApi.GetFeatureValue<VampirismConfig>(attacker, FeatureKey);
        if (config == null) return HookResult.Continue;

        if (config.GiveHealthMode != GiveHealthMode.OnDamage) return HookResult.Continue;

        int heal = 0;

        if (config.HealthReturnMode == HealthMode.Percent)
        {
            float percent = config.Percent;
            if (percent <= 0.0f) return HookResult.Continue;

             heal = (int)MathF.Round(dmgHealth * (percent / 100.0f));
            if (heal <= 0) return HookResult.Continue;
        }

        if (config.HealthReturnMode == HealthMode.Flat)
        {
            heal = config.Flat;
            if (heal <= 0) return HookResult.Continue;
        }

        var controller = attacker.Controller as CCSPlayerController;
        if (controller == null || !controller.IsValid) return HookResult.Continue;

        var pawn = controller.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid) return HookResult.Continue;

        var newHealth = pawn.Health + heal;
        if (newHealth <= 0)
            SetNewHealthForPawn(pawn, newHealth);

        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult HandlePlayerDeath(EventPlayerDeath @event)
    {
        if (_vipApi == null) return HookResult.Continue;
        var attackerId = @event.Attacker;
        var victimId = @event.UserId;
        if (attackerId <= 0) return HookResult.Continue;

        var attacker = Core.PlayerManager.GetPlayer(attackerId);
        if (victimId == attackerId) return HookResult.Continue;
        if (attacker == null || attacker.IsFakeClient || !attacker.IsValid) return HookResult.Continue;
        if (!_vipApi.IsClientVip(attacker)) return HookResult.Continue;
        if (_vipApi.GetPlayerFeatureState(attacker, FeatureKey) != FeatureState.Enabled) return HookResult.Continue;

        var config = _vipApi.GetFeatureValue<VampirismConfig>(attacker, FeatureKey);
        if (config == null) return HookResult.Continue;

        if (config.GiveHealthMode != GiveHealthMode.OnKill) return HookResult.Continue;

        if (config.HealthReturnMode != HealthMode.Flat) return HookResult.Continue;

        var controller = attacker.Controller as CCSPlayerController;
        if (controller == null || !controller.IsValid) return HookResult.Continue;

        var pawn = controller.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid) return HookResult.Continue;

        int giveHealth = config.Flat;
        if (giveHealth == 0) return HookResult.Continue;

        var newHealth = pawn.Health + giveHealth;
        if (newHealth > 0)
            SetNewHealthForPawn(pawn, newHealth);

        return HookResult.Continue;
    }

    private void SetNewHealthForPawn(CCSPlayerPawn pawn, int newHealth)
    {
        if (newHealth > pawn.MaxHealth)
            newHealth = pawn.MaxHealth;

        pawn.Health = newHealth;
        pawn.HealthUpdated();
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
    public GiveHealthMode GiveHealthMode { get; set; } = GiveHealthMode.OnDamage;
    public HealthMode HealthReturnMode { get; set; } = HealthMode.Percent;
    public float Percent { get; set; } = 0.0f;
    public int Flat { get; set; } = 0;
}

public enum HealthMode
{
    Percent = 0,
    Flat = 1
}

public enum GiveHealthMode
{
    OnDamage = 0,
    OnKill = 1
}