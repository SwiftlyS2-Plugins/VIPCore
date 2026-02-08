using System.Collections.Concurrent;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.SchemaDefinitions;
using VIPCore.Contract;

namespace VIP_SmokeColor;

[PluginMetadata(Id = "VIP_SmokeColor", Version = "1.0.0", Name = "VIP_SmokeColor", Author = "aga", Description = "No description.")]
public partial class VIP_SmokeColor : BasePlugin {
  private const string FeatureKey = "vip.smokecolor";
  private const string SmokeProjectileDesignerName = "smokegrenade_projectile";
  private const float ReapplyIntervalSeconds = 0.25f;
  private const float ReapplyDurationSeconds = 22.0f;

  private IVipCoreApiV1? _vipApi;
  private bool _isFeatureRegistered;

  private readonly ConcurrentDictionary<uint, Vector> _smokeColorsByEntityIndex = new();

  public VIP_SmokeColor(ISwiftlyCore core) : base(core)
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
    Core.Event.OnEntityCreated += OnEntityCreated;
    RegisterVipFeaturesWhenReady();
  }

  public override void Unload() {
    Core.Event.OnEntityCreated -= OnEntityCreated;

    if (_vipApi != null)
    {
      _vipApi.OnCoreReady -= RegisterVipFeatures;

      if (_isFeatureRegistered)
        _vipApi.UnregisterFeature(FeatureKey);
    }

    _smokeColorsByEntityIndex.Clear();
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
      displayNameResolver: p => "Smoke Color"
    );

    _isFeatureRegistered = true;
  }

  private void OnEntityCreated(IOnEntityCreatedEvent @event)
  {
    if (_vipApi == null) return;

    var entity = @event.Entity;
    if (!entity.IsValid) return;
    if (entity.DesignerName != SmokeProjectileDesignerName) return;

    var smoke = Core.EntitySystem.GetEntityByIndex<CSmokeGrenadeProjectile>(entity.Index);
    if (smoke == null || !smoke.IsValid) return;

    Core.Scheduler.NextTick(() => InitializeAndScheduleReapply(smoke));
  }

  private void InitializeAndScheduleReapply(CSmokeGrenadeProjectile smoke)
  {
    if (_vipApi == null) return;
    if (smoke == null || !smoke.IsValid) return;

    var throwerPawn = smoke.Thrower.Value;
    if (throwerPawn == null || !throwerPawn.IsValid) return;

    var player = Core.PlayerManager.GetPlayerFromPawn(throwerPawn);
    if (player == null || player.IsFakeClient || !player.IsValid) return;

    if (!_vipApi.IsClientVip(player)) return;
    if (_vipApi.GetPlayerFeatureState(player, FeatureKey) != FeatureState.Enabled) return;

    var color = _vipApi.GetFeatureValue<List<int>>(player, FeatureKey);
    int r = -1, g = -1, b = -1;
    if (color != null)
    {
      if (color.Count > 0) r = color[0];
      if (color.Count > 1) g = color[1];
      if (color.Count > 2) b = color[2];
    }

    var desired = new Vector(
      ClampColorComponent(r),
      ClampColorComponent(g),
      ClampColorComponent(b)
    );

    _smokeColorsByEntityIndex[(uint)smoke.Index] = desired;
    ApplySmokeColor(smoke, desired);
    ScheduleReapply(smoke, 0.0f);
  }

  private void ScheduleReapply(CSmokeGrenadeProjectile smoke, float elapsed)
  {
    if (elapsed >= ReapplyDurationSeconds) return;
    if (smoke == null || !smoke.IsValid) return;

    Core.Scheduler.DelayBySeconds(ReapplyIntervalSeconds, () =>
    {
      if (smoke == null || !smoke.IsValid) return;

      if (_smokeColorsByEntityIndex.TryGetValue((uint)smoke.Index, out var desired))
        ApplySmokeColor(smoke, desired);

      ScheduleReapply(smoke, elapsed + ReapplyIntervalSeconds);
    });
  }

  private static float ClampColorComponent(int value)
  {
    if (value == -1) return Random.Shared.Next(0, 256);
    if (value < 0) return 0;
    if (value > 255) return 255;
    return value;
  }

  private static void ApplySmokeColor(CSmokeGrenadeProjectile smoke, Vector desired)
  {
    smoke.SmokeColor.X = desired.X;
    smoke.SmokeColor.Y = desired.Y;
    smoke.SmokeColor.Z = desired.Z;
    smoke.SmokeColorUpdated();
  }
}