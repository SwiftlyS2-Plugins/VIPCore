using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using VIPCore.Contract;

namespace VIP_Health;

[PluginMetadata(Id = "VIP_Health", Version = "1.0.0", Name = "VIP_Health", Author = "aga", Description = "No description.")]
public partial class VIP_Health : BasePlugin {
  private const string FeatureKey = "vip.health";
  private const float ApplyRetryDelaySeconds = 0.05f;
  private const int ApplyMaxAttempts = 10;

  private IVipCoreApiV1? _vipApi;
  private bool _isFeatureRegistered;

  public VIP_Health(ISwiftlyCore core) : base(core)
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
      displayNameResolver: p => Core.Translation.GetPlayerLocalizer(p)["vip.health"]
    );

    _isFeatureRegistered = true;
    _vipApi.OnPlayerSpawn += OnVipPlayerSpawn;
  }

  private void OnVipPlayerSpawn(IPlayer player)
  {
    TryApplyHealth(player);
  }

  private HookResult OnPlayerSpawn(EventPlayerSpawn @event)
  {
    var player = @event.UserIdPlayer;
    if (player == null) return HookResult.Continue;

    TryApplyHealth(player);
    return HookResult.Continue;
  }

  private void TryApplyHealth(IPlayer player)
  {
    if (_vipApi == null) return;
    if (player.IsFakeClient || !player.IsValid) return;
    if (!_vipApi.IsClientVip(player)) return;
    if (_vipApi.GetPlayerFeatureState(player, FeatureKey) != FeatureState.Enabled) return;

    var healthValue = 0;
    try
    {
      var config = _vipApi.GetFeatureValue<HealthConfig>(player, FeatureKey);
      healthValue = config?.Health ?? 0;
    }
    catch
    {
      healthValue = 0;
    }

    if (healthValue <= 0) return;

    ScheduleApplyAttempt(player, healthValue, attempt: 1);
  }

  private void ScheduleApplyAttempt(IPlayer player, int healthValue, int attempt)
  {
    Core.Scheduler.NextTick(() =>
    {
      if (ApplyHealth(player, healthValue)) return;
      if (attempt >= ApplyMaxAttempts) return;

      Core.Scheduler.DelayBySeconds(ApplyRetryDelaySeconds, () => ScheduleApplyAttempt(player, healthValue, attempt + 1));
    });
  }

  private bool ApplyHealth(IPlayer player, int healthValue)
  {
    var controller = player.Controller as CCSPlayerController;
    if (controller == null || !controller.IsValid) return false;

    var pawn = controller.PlayerPawn.Value;
    if (pawn == null || !pawn.IsValid) return false;

    pawn.Health = healthValue;
    pawn.HealthUpdated();

    pawn.MaxHealth = healthValue;
    pawn.MaxHealthUpdated();

    return true;
  }
}

public class HealthConfig
{
  public int Health { get; set; } = 0;
}