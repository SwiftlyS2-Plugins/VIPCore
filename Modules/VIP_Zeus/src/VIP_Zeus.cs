using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using VIPCore.Contract;

namespace VIP_Zeus;

[PluginMetadata(Id = "VIP_Zeus", Version = "1.0.0", Name = "VIP_Zeus", Author = "aga", Description = "No description.")]
public partial class VIP_Zeus : BasePlugin {
  private const string FeatureKey = "vip.zeus";
  private const string TaserDesignerName = "weapon_taser";

  private const int GiveMaxAttempts = 5;
  private const float GiveRetryDelaySeconds = 0.1f;

  private IVipCoreApiV1? _vipApi;
  private bool _isFeatureRegistered;

  public VIP_Zeus(ISwiftlyCore core) : base(core)
  {
  }

  public override void ConfigureSharedInterface(IInterfaceManager interfaceManager) {
  }

  public override void UseSharedInterface(IInterfaceManager interfaceManager) {
    _vipApi = null;
    _isFeatureRegistered = false;

    if (interfaceManager.HasSharedInterface("VIPCore.Api.v1"))
      _vipApi = interfaceManager.GetSharedInterface<IVipCoreApiV1>("VIPCore.Api.v1");

    RegisterVipFeaturesWhenReady();
  }

  public override void Load(bool hotReload) {
    Core.GameEvent.HookPost<EventPlayerSpawn>(OnPlayerSpawnEvent);
    RegisterVipFeaturesWhenReady();
  }

  public override void Unload() {
    if (_vipApi != null)
    {
      _vipApi.OnCoreReady -= RegisterVipFeatures;
      _vipApi.PlayerLoaded -= OnPlayerLoaded;

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
      displayNameResolver: p => "Zeus"
    );

    _isFeatureRegistered = true;
    _vipApi.PlayerLoaded += OnPlayerLoaded;
  }

  private void OnPlayerLoaded(IPlayer player, string group)
  {
    ScheduleGiveAttempt(player, attempt: 1);
  }

  private HookResult OnPlayerSpawnEvent(EventPlayerSpawn @event)
  {
    var player = @event.UserIdPlayer;
    if (player == null || player.IsFakeClient || !player.IsValid) return HookResult.Continue;

    ScheduleGiveAttempt(player, attempt: 1);
    return HookResult.Continue;
  }

  private void ScheduleGiveAttempt(IPlayer player, int attempt)
  {
    Core.Scheduler.NextTick(() =>
    {
      if (TryGiveTaser(player)) return;
      if (attempt >= GiveMaxAttempts) return;

      Core.Scheduler.DelayBySeconds(GiveRetryDelaySeconds, () => ScheduleGiveAttempt(player, attempt + 1));
    });
  }

  private bool TryGiveTaser(IPlayer player)
  {
    if (_vipApi == null) return false;
    if (player.IsFakeClient || !player.IsValid) return false;

    if (!_vipApi.IsClientVip(player)) return false;
    if (_vipApi.GetPlayerFeatureState(player, FeatureKey) != FeatureState.Enabled) return false;

    var controller = player.Controller as CCSPlayerController;
    if (controller == null || !controller.IsValid) return false;
    if (!controller.PawnIsAlive) return false;

    if (controller.TeamNum != 2 && controller.TeamNum != 3) return false;

    var pawn = controller.PlayerPawn.Value;
    if (pawn == null || !pawn.IsValid) return false;

    var weaponServices = pawn.WeaponServices;
    if (weaponServices == null) return false;

    var alreadyHas = weaponServices.MyValidWeapons.Any(w => w.Entity?.DesignerName == TaserDesignerName);
    if (alreadyHas) return true;

    var itemServices = pawn.ItemServices;
    if (itemServices == null) return false;

    itemServices.GiveItem(TaserDesignerName);

    return true;
  }
}