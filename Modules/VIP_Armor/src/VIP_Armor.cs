using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using VIPCore.Contract;

namespace VIP_Armor;

[PluginMetadata(Id = "VIP_Armor", Version = "1.0.0", Name = "VIP_Armor", Author = "aga", Description = "No description.")]
public partial class VIP_Armor : BasePlugin {
  private const string FeatureKey = "vip.armor";
  private const float ApplyRetryDelaySeconds = 0.05f;
  private const int ApplyMaxAttempts = 10;

  private IVipCoreApiV1? _vipApi;
  private bool _isFeatureRegistered;

  public VIP_Armor(ISwiftlyCore core) : base(core)
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
        return;

      _vipApi = interfaceManager.GetSharedInterface<IVipCoreApiV1>("VIPCore.Api.v1");

      RegisterVipFeaturesWhenReady();
    }
    catch
    {
    }
  }

  public override void Load(bool hotReload) {
    Core.GameEvent.HookPost<EventPlayerSpawn>(OnPlayerSpawn);
    RegisterVipFeaturesWhenReady();
  }

  public override void Unload() {
    if (_vipApi != null)
    {
      _vipApi.OnCoreReady -= RegisterVipFeatures;
      _vipApi.OnPlayerSpawn -= OnVipPlayerSpawn;

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
      displayNameResolver: p => Core.Translation.GetPlayerLocalizer(p)["vip.armor"]
    );

    _isFeatureRegistered = true;
    _vipApi.OnPlayerSpawn += OnVipPlayerSpawn;
  }

  private void OnVipPlayerSpawn(IPlayer player)
  {
    TryApplyArmor(player);
  }

  private HookResult OnPlayerSpawn(EventPlayerSpawn @event)
  {
    var player = @event.UserIdPlayer;
    if (player == null) return HookResult.Continue;

    TryApplyArmor(player);
    return HookResult.Continue;
  }

  private void TryApplyArmor(IPlayer player)
  {
    if (_vipApi == null) return;
    if (player.IsFakeClient || !player.IsValid) return;
    if (!_vipApi.IsClientVip(player)) return;
    if (_vipApi.GetPlayerFeatureState(player, FeatureKey) != FeatureState.Enabled) return;

    var armorValue = 0;
    try
    {
      var config = _vipApi.GetFeatureValue<ArmorConfig>(player, FeatureKey);
      armorValue = config?.Armor ?? 0;
    }
    catch
    {
      armorValue = 0;
    }

    if (armorValue <= 0) return;

    ScheduleApplyAttempt(player, armorValue, attempt: 1);
  }

  private void ScheduleApplyAttempt(IPlayer player, int armorValue, int attempt)
  {
    Core.Scheduler.NextTick(() =>
    {
      if (ApplyArmor(player, armorValue)) return;
      if (attempt >= ApplyMaxAttempts) return;

      Core.Scheduler.DelayBySeconds(ApplyRetryDelaySeconds, () => ScheduleApplyAttempt(player, armorValue, attempt + 1));
    });
  }

  private bool ApplyArmor(IPlayer player, int armorValue)
  {
    var controller = player.Controller as CCSPlayerController;
    if (controller == null || !controller.IsValid) return false;

    var pawn = controller.PlayerPawn.Value;
    if (pawn == null || !pawn.IsValid) return false;

    var itemServices = pawn.ItemServices;
    if (itemServices != null)
    {
      itemServices.HasHelmet = true;
      itemServices.HasHelmetUpdated();
    }

    pawn.ArmorValue = armorValue;
    pawn.ArmorValueUpdated();

    return true;
  }
}

public class ArmorConfig
{
  public int Armor { get; set; } = 0;
}