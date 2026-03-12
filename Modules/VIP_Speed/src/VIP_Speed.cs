using Microsoft.Extensions.DependencyInjection;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.SchemaDefinitions;
using VIPCore.Contract;
using SwiftlyS2.Shared.GameEvents;

namespace VIP_Speed;

[PluginMetadata(Id = "VIP_Speed", Version = "1.0.0", Name = "VIP_Speed", Author = "aga", Description = "No description.")]
public partial class VIP_Speed : BasePlugin {
  private const string FeatureKey = "vip.speed";

  private IVipCoreApiV1? _vipApi;
  private bool _isFeatureRegistered;

  public VIP_Speed(ISwiftlyCore core) : base(core)
  {
  }

  public override void ConfigureSharedInterface(IInterfaceManager interfaceManager) {
  }

  public override void UseSharedInterface(IInterfaceManager interfaceManager) {
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

  public override void Load(bool hotReload) {
    Core.GameEvent.HookPost<EventPlayerSpawn>(OnPlayerSpawn);
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
          displayNameResolver: p => Core.Translation.GetPlayerLocalizer(p)["vip.speed"]
      );

      _isFeatureRegistered = true;
      _vipApi.PlayerLoaded += OnPlayerLoaded;
  }

  public class SpeedConfig
  {
      public float Speed { get; set; } = 1.0f;
  }

  private void OnPlayerLoaded(IPlayer player, string group)
  {
      ApplySpeed(player);
  }

  private HookResult OnPlayerHurt(EventPlayerHurt @event)
  {
      if (_vipApi == null) return HookResult.Continue;

      var victimId = @event.UserId;
      if (victimId <= 0) return HookResult.Continue;

      var victim = Core.PlayerManager.GetPlayer(victimId);
      if (victim == null) return HookResult.Continue;

      ApplySpeed(victim);

      return HookResult.Continue;
  }

  private HookResult OnPlayerSpawn(EventPlayerSpawn @event)
  {
      if (_vipApi == null) return HookResult.Continue;

      var player = @event.UserIdPlayer;
      if (player == null) return HookResult.Continue;

      Core.Scheduler.NextTick(() => ApplySpeed(player));

      return HookResult.Continue;
  }

  private void ApplySpeed(IPlayer player)
  {
      if (_vipApi == null) return;
      if (player.IsFakeClient || !player.IsValid) return;
      if (!_vipApi.IsClientVip(player)) return;
      if (_vipApi.GetPlayerFeatureState(player, FeatureKey) != FeatureState.Enabled) return;

      var config = _vipApi.GetFeatureValue<SpeedConfig>(player, FeatureKey);
      if (config == null || config.Speed <= 0) return;

      var pawn = player.PlayerPawn;
      if (pawn == null || !pawn.IsValid) return;

      pawn.VelocityModifier = config.Speed;
      pawn.VelocityModifierUpdated();
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
}