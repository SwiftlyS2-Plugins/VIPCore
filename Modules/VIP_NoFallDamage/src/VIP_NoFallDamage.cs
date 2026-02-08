using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using VIPCore.Contract;

namespace VIP_NoFallDamage;

[PluginMetadata(Id = "VIP_NoFallDamage", Version = "1.0.0", Name = "VIP_NoFallDamage", Author = "aga", Description = "No description.")]
public partial class VIP_NoFallDamage : BasePlugin {
  private const string FeatureKey = "vip.nofalldamage";

  private IVipCoreApiV1? _vipApi;
  private bool _isFeatureRegistered;

  public VIP_NoFallDamage(ISwiftlyCore core) : base(core)
  {
  }

  public override void ConfigureSharedInterface(IInterfaceManager interfaceManager) {
  }

  public override void UseSharedInterface(IInterfaceManager interfaceManager) {
    _vipApi = null;
    _isFeatureRegistered = false;

    try
    {
      if (!interfaceManager.HasSharedInterface("VIPCore.Api.v1"))
      {
        return;
      }

      _vipApi = interfaceManager.GetSharedInterface<IVipCoreApiV1>("VIPCore.Api.v1");

      RegisterVipFeaturesWhenReady();
    }
    catch
    {
    }
  }

  public override void Load(bool hotReload) {
    Core.GameEvent.HookPre<EventPlayerFalldamage>(OnPlayerFallDamage);
    Core.Event.OnEntityTakeDamage += OnEntityTakeDamage;
    RegisterVipFeaturesWhenReady();
  }

  public override void Unload() {
    Core.Event.OnEntityTakeDamage -= OnEntityTakeDamage;
    if (_vipApi != null)
    {
      _vipApi.OnCoreReady -= RegisterVipFeatures;
      if (_isFeatureRegistered)
        _vipApi.UnregisterFeature(FeatureKey);
    }
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
      displayNameResolver: p => Core.Translation.GetPlayerLocalizer(p)["vip.nofalldamage"]
    );

    _isFeatureRegistered = true;
  }

  private HookResult OnPlayerFallDamage(EventPlayerFalldamage @event)
  {
    if (_vipApi == null) return HookResult.Continue;

    var player = @event.UserIdPlayer;
    if (player == null || player.IsFakeClient || !player.IsValid) return HookResult.Continue;

    if (!_vipApi.IsClientVip(player)) return HookResult.Continue;
    if (_vipApi.GetPlayerFeatureState(player, FeatureKey) != FeatureState.Enabled) return HookResult.Continue;

    var dmg = @event.Damage;
    if (dmg <= 0.0f) return HookResult.Continue;

    @event.Damage = 0.0f;
    return HookResult.Continue;
  }

  private void OnEntityTakeDamage(IOnEntityTakeDamageEvent @event)
  {
    if (_vipApi == null) return;

    ref var info = ref @event.Info;
    if (!info.DamageType.HasFlag(DamageTypes_t.DMG_FALL)) return;

    var entityInstance = @event.Entity;
    if (!entityInstance.IsValid) return;
    if (entityInstance.DesignerName != "player") return;

    var pawn = Core.EntitySystem.GetEntityByIndex<CCSPlayerPawn>(entityInstance.Index);
    if (pawn == null || !pawn.IsValid) return;

    var player = Core.PlayerManager.GetPlayerFromPawn(pawn);
    if (player == null || player.IsFakeClient || !player.IsValid) return;

    if (!_vipApi.IsClientVip(player)) return;
    if (_vipApi.GetPlayerFeatureState(player, FeatureKey) != FeatureState.Enabled) return;

    @event.Result = HookResult.Stop;
  }
}