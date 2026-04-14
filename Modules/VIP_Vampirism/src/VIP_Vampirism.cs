using System;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.SchemaDefinitions;
using VIPCore.Contract;
using SwiftlyS2.Shared.GameEvents;
using SwiftlyS2.Shared.Players;

namespace VIP_Vampirism;

[PluginMetadata(Id = "VIP_Vampirism", Version = "1.0.0", Name = "VIP_Vampirism", Author = "aga, shmitz", Description = "Retruns HP to attacker based on configured logic")]
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
        catch (Exception ex)
        {
            Core.Logger.LogError(ex, "[VIP_Vampirism] Failed to load VIPCore shared interface");
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
        if (attackerId <= 0 || @event.UserId == attackerId) return HookResult.Continue;

        var attacker = Core.PlayerManager.GetPlayer(attackerId);
        if (attacker == null || attacker.IsFakeClient || !attacker.IsValid) return HookResult.Continue;

        if (!_vipApi.IsClientVip(attacker)) return HookResult.Continue;
        if (_vipApi.GetPlayerFeatureState(attacker, FeatureKey) != FeatureState.Enabled) return HookResult.Continue;

        var dmgHealth = @event.DmgHealth;
        if (dmgHealth <= 0) return HookResult.Continue;

        var config = _vipApi.GetFeatureValue<VampirismConfig>(attacker, FeatureKey);
        if (config == null || config.GiveHealthMode != GiveHealthMode.OnDamage) return HookResult.Continue;

        int heal = config.HealthReturnMode switch
        {
            HealthMode.Percent when config.Percent > 0f => (int)MathF.Round(dmgHealth * (config.Percent / 100.0f)),
            HealthMode.Flat => config.Flat,
            _ => 0
        };

        if (heal <= 0) return HookResult.Continue;

        var controller = attacker.Controller as CCSPlayerController;
        if (controller == null || !controller.IsValid) return HookResult.Continue;

        var pawn = controller.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid || pawn.Health <= 0) return HookResult.Continue;

        SetNewHealthForPawn(pawn, pawn.Health + heal);

        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult HandlePlayerDeath(EventPlayerDeath @event)
    {
        if (_vipApi == null) return HookResult.Continue;

        var attackerId = @event.Attacker;
        if (attackerId <= 0 || @event.UserId == attackerId) return HookResult.Continue;

        var attacker = Core.PlayerManager.GetPlayer(attackerId);
        if (attacker == null || attacker.IsFakeClient || !attacker.IsValid) return HookResult.Continue;
        if (!_vipApi.IsClientVip(attacker)) return HookResult.Continue;
        if (_vipApi.GetPlayerFeatureState(attacker, FeatureKey) != FeatureState.Enabled) return HookResult.Continue;

        var config = _vipApi.GetFeatureValue<VampirismConfig>(attacker, FeatureKey);
        if (config == null || config.GiveHealthMode != GiveHealthMode.OnKill) return HookResult.Continue;

        var controller = attacker.Controller as CCSPlayerController;
        if (controller == null || !controller.IsValid) return HookResult.Continue;

        var pawn = controller.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid || pawn.Health <= 0) return HookResult.Continue;

        int giveHealth = config.HealthReturnMode switch
        {
            HealthMode.Percent when config.Percent > 0f => (int)MathF.Round(pawn.MaxHealth * (config.Percent / 100.0f)),
            HealthMode.Flat => config.Flat,
            _ => 0
        };

        if (giveHealth <= 0) return HookResult.Continue;

        int clampedNewHealth = Math.Min(pawn.Health + giveHealth, pawn.MaxHealth);
        int gainedHealth = clampedNewHealth - pawn.Health;

        if (gainedHealth > 0)
        {
            SetNewHealthForPawn(pawn, clampedNewHealth);

            var victim = Core.PlayerManager.GetPlayer(@event.UserId);
            var victimName = victim?.Controller?.PlayerName ?? "Unknown";

            var localizer = Core.Translation.GetPlayerLocalizer(attacker);
            attacker.SendMessage(MessageType.Chat, localizer["vampirism.GainedOnKill", gainedHealth, victimName]);
        }

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
