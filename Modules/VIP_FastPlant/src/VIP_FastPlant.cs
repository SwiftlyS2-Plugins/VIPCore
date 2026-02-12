using System;
using System.Collections.Generic;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using VIPCore.Contract;

namespace VIP_FastPlant;

[PluginMetadata(Id = "VIP_FastPlant", Version = "1.0.0", Name = "VIP_FastPlant", Author = "aga", Description = "No description.")]
public partial class VIP_FastPlant : BasePlugin {
  private const string FeatureKey = "vip.fastplant";

  private IVipCoreApiV1? _vipApi;
  private bool _isFeatureRegistered;

  private long _plantTokenCounter;
  private readonly Dictionary<uint, long> _plantTokenByControllerIndex = new();

  public VIP_FastPlant(ISwiftlyCore core) : base(core)
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
    Core.GameEvent.HookPost<EventBombBeginplant>(OnBombBeginPlant);
    Core.GameEvent.HookPost<EventBombPlanted>(OnBombPlanted);
    RegisterVipFeaturesWhenReady();
  }

  public override void Unload() {
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
      displayNameResolver: p => "Fast Plant"
    );

    _isFeatureRegistered = true;
  }

  private HookResult OnBombBeginPlant(EventBombBeginplant @event)
  {
    if (_vipApi == null) return HookResult.Continue;

    var player = @event.UserIdPlayer;
    if (player == null || !player.IsValid || player.IsFakeClient) return HookResult.Continue;

    if (!_vipApi.IsClientVip(player)) return HookResult.Continue;
    if (_vipApi.GetPlayerFeatureState(player, FeatureKey) != FeatureState.Enabled) return HookResult.Continue;

    var controller = player.Controller as CCSPlayerController;
    if (controller == null || !controller.IsValid) return HookResult.Continue;
    if (!controller.PawnIsAlive) return HookResult.Continue;

    var pawn = controller.PlayerPawn.Value;
    if (pawn == null || !pawn.IsValid) return HookResult.Continue;

    var weaponServices = pawn.WeaponServices;
    if (weaponServices == null) return HookResult.Continue;

    var activeWeapon = weaponServices.ActiveWeapon.Value;
    if (activeWeapon == null || !activeWeapon.IsValid) return HookResult.Continue;

    if (!activeWeapon.DesignerName.Contains("c4", StringComparison.OrdinalIgnoreCase))
      return HookResult.Continue;

    var c4 = Core.EntitySystem.GetEntityByIndex<CC4>(activeWeapon.Index);
    if (c4 == null) return HookResult.Continue;

    var pawnBase = pawn as CCSPlayerPawnBase;
    if (pawnBase == null) return HookResult.Continue;

    var currentTime = Core.Engine.GlobalVars.CurrentTime;

    var multiplier = 0.5f;
    var durationOverride = 0;
    var plantTimeSeconds = 3.0f;
    try
    {
      var config = _vipApi.GetFeatureValue<FastPlantConfig>(player, FeatureKey);
      if (config != null)
      {
        if (config.Multiplier > 0 && config.Multiplier <= 1.0f)
          multiplier = config.Multiplier;
        if (config.Duration > 0)
          durationOverride = config.Duration;
        if (config.PlantTimeSeconds > 0)
          plantTimeSeconds = config.PlantTimeSeconds;
      }
    }
    catch
    {
    }

    var originalDuration = pawnBase.ProgressBarDuration;
    var newDuration = durationOverride > 0
      ? durationOverride
      : Math.Max(1, (int)MathF.Round(originalDuration * multiplier));

    pawnBase.ProgressBarStartTime = currentTime;
    pawnBase.ProgressBarStartTimeUpdated();

    pawnBase.ProgressBarDuration = newDuration;
    pawnBase.ProgressBarDurationUpdated();

    c4.BombPlacedAnimation = false;
    c4.BombPlacedAnimationUpdated();

    var effectivePlantTime = durationOverride > 0 ? (float)durationOverride : (plantTimeSeconds * multiplier);
    if (effectivePlantTime <= 0.001f)
      effectivePlantTime = 0.001f;

    var backdate = Math.Max(0.0f, plantTimeSeconds - effectivePlantTime);
    c4.ArmedTime.Value = currentTime - backdate;
    c4.ArmedTimeUpdated();

    var token = ++_plantTokenCounter;
    _plantTokenByControllerIndex[controller.Index] = token;

    Core.Scheduler.DelayBySeconds(effectivePlantTime + 0.25f, () =>
    {
      if (controller == null || !controller.IsValid) return;
      if (_plantTokenByControllerIndex.TryGetValue(controller.Index, out var t) && t == token)
        ResetProgressBar(controller);
    });

    return HookResult.Continue;
  }

  private HookResult OnBombPlanted(EventBombPlanted @event)
  {
    var player = @event.UserIdPlayer;
    if (player == null || !player.IsValid || player.IsFakeClient) return HookResult.Continue;

    var controller = player.Controller as CCSPlayerController;
    if (controller == null || !controller.IsValid) return HookResult.Continue;

    _plantTokenByControllerIndex.Remove(controller.Index);
    ResetProgressBar(controller);

    return HookResult.Continue;
  }

  private void ResetProgressBar(CCSPlayerController controller)
  {
    var pawn = controller.PlayerPawn.Value;
    if (pawn == null || !pawn.IsValid) return;

    var pawnBase = pawn as CCSPlayerPawnBase;
    if (pawnBase == null) return;

    pawnBase.ProgressBarStartTime = 0;
    pawnBase.ProgressBarStartTimeUpdated();

    pawnBase.ProgressBarDuration = 0;
    pawnBase.ProgressBarDurationUpdated();
  }
}

public class FastPlantConfig
{
  public float Multiplier { get; set; } = 0.5f;
  public int Duration { get; set; } = 0;
  public float PlantTimeSeconds { get; set; } = 3.0f;
}