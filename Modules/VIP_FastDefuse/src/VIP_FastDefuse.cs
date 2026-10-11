using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using VIPCore.Contract;

namespace VIP_FastDefuse;

[PluginMetadata(Id = "VIP_FastDefuse", Version = "1.0.0", Name = "VIP_FastDefuse", Author = "aga", Description = "No description.")]
public partial class VIP_FastDefuse : BasePlugin {
  private const string FeatureKey = "vip.fastdefuse";
  private const float DefaultDefuseTimeNoKitSeconds = 10.0f;
  private const float DefaultDefuseTimeKitSeconds = 5.0f;

  private IVipCoreApiV1? _vipApi;
  private bool _isFeatureRegistered;

  private long _defuseTokenCounter;
  private readonly Dictionary<uint, long> _defuseTokenByControllerIndex = new();

  public VIP_FastDefuse(ISwiftlyCore core) : base(core)
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
    catch (Exception ex)
    {
      Core.Logger.LogError(ex, "[VIP_FastDefuse] Failed to get VIPCore shared interface");
    }
  }

  public override void Load(bool hotReload) {
    Core.Logger.LogDebug("[VIP_FastDefuse] Loaded, hooking bomb events");
    Core.GameEvent.HookPost<EventBombBegindefuse>(OnBombBeginDefuse);
    Core.GameEvent.HookPost<EventBombDefused>(OnBombDefused);
    Core.GameEvent.HookPost<EventBombAbortdefuse>(OnBombAbortDefuse);
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
      displayNameResolver: p => "Fast Defuse"
    );

    _isFeatureRegistered = true;
  }

  private HookResult OnBombBeginDefuse(EventBombBegindefuse @event)
  {
    Core.Logger.LogDebug("[VIP_FastDefuse] bomb_begindefuse fired (vipApi null: {Null})", _vipApi == null);
    if (_vipApi == null) return HookResult.Continue;

    var player = @event.UserIdPlayer;
    var controller = @event.UserIdController ?? player?.Controller as CCSPlayerController;
    if (controller == null || !controller.IsValid)
    {
      Core.Logger.LogDebug("[VIP_FastDefuse] No valid controller in event");
      return HookResult.Continue;
    }

    if (player == null || !player.IsValid)
      player = Core.PlayerManager.GetPlayerFromController(controller);

    if (player == null || !player.IsValid || player.IsFakeClient)
    {
      Core.Logger.LogDebug("[VIP_FastDefuse] No valid player (or fake client)");
      return HookResult.Continue;
    }

    var pawn = controller.PlayerPawn.Value;
    if (pawn == null || !pawn.IsValid)
    {
      Core.Logger.LogDebug("[VIP_FastDefuse] No valid pawn");
      return HookResult.Continue;
    }

    var isVip = _vipApi.IsClientVip(player);
    var state = _vipApi.GetPlayerFeatureState(player, FeatureKey);
    Core.Logger.LogDebug("[VIP_FastDefuse] {PlayerName}: vip={IsVip}, featureState={State}", controller.PlayerName, isVip, state);
    if (!isVip || state != FeatureState.Enabled) return HookResult.Continue;

    var pawnBase = pawn as CCSPlayerPawnBase;
    if (pawnBase == null) return HookResult.Continue;

    var plantedC4 = GetPlantedC4();
    if (plantedC4 == null || !plantedC4.IsValid || plantedC4.CannotBeDefused)
    {
      Core.Logger.LogDebug("[VIP_FastDefuse] Planted C4 missing/invalid/cannot be defused");
      return HookResult.Continue;
    }

    var currentTime = Core.Engine.GlobalVars.CurrentTime;
    var defaultDefuseSeconds = @event.HasKit ? DefaultDefuseTimeKitSeconds : DefaultDefuseTimeNoKitSeconds;
    var remaining = defaultDefuseSeconds;

    try
    {
      var defuseCountdown = plantedC4.DefuseCountDown.Value;
      if (defuseCountdown > currentTime)
        remaining = defuseCountdown - currentTime;
    }
    catch
    {
    }

    if (remaining <= 0)
      remaining = defaultDefuseSeconds;

    var multiplier = 0.5f;
    var secondsOverride = 0.0f;
    try
    {
      var config = _vipApi.GetFeatureValue<FastDefuseConfig>(player, FeatureKey);
      if (config != null)
      {
        if (config.Multiplier > 0)
        {
          if (config.Multiplier <= 1.0f)
            multiplier = config.Multiplier;
          else
            multiplier = 1.0f / config.Multiplier;
        }
        if (config.Seconds > 0)
          secondsOverride = config.Seconds;
      }
    }
    catch
    {
    }

    var newRemaining = secondsOverride > 0 ? secondsOverride : (remaining * multiplier);
    if (newRemaining < 0.25f)
      newRemaining = 0.25f;

    Core.Logger.LogDebug("[VIP_FastDefuse] {PlayerName} started defuse (kit={HasKit}), remaining={Remaining:F2}s, newRemaining={NewRemaining:F2}s",
      controller.PlayerName, @event.HasKit, remaining, newRemaining);

    var token = ++_defuseTokenCounter;
    _defuseTokenByControllerIndex[controller.Index] = token;

    var startTime = currentTime;
    var endTime = startTime + newRemaining;
    var barDuration = Math.Max(1, (int)MathF.Ceiling(newRemaining));

    bool IsActive() =>
      controller != null && controller.IsValid &&
      plantedC4 != null && plantedC4.IsValid &&
      _defuseTokenByControllerIndex.TryGetValue(controller.Index, out var t) && t == token;

    void Enforce()
    {
      if (!IsActive()) return;

      plantedC4.DefuseLength = newRemaining;
      plantedC4.DefuseLengthUpdated();

      plantedC4.DefuseCountDown.Value = endTime;
      plantedC4.DefuseCountDownUpdated();

      pawnBase.ProgressBarStartTime = endTime - barDuration;
      pawnBase.ProgressBarStartTimeUpdated();
      pawnBase.ProgressBarDuration = barDuration;
      pawnBase.ProgressBarDurationUpdated();
    }

    void Tick()
    {
      if (!IsActive()) return;
      if (Core.Engine.GlobalVars.CurrentTime > endTime + 1.0f) return;

      Enforce();
      Core.Scheduler.DelayBySeconds(0.05f, Tick);
    }

    TrySetMember(plantedC4, "ProgressBarTime", barDuration);
    Enforce();
    Core.Scheduler.NextTick(Tick);

    return HookResult.Continue;
  }

  private HookResult OnBombDefused(EventBombDefused @event)
  {
    var player = @event.UserIdPlayer;
    if (player == null || !player.IsValid || player.IsFakeClient) return HookResult.Continue;

    var controller = player.Controller as CCSPlayerController;
    if (controller == null || !controller.IsValid) return HookResult.Continue;

    _defuseTokenByControllerIndex.Remove(controller.Index);
    ResetProgressBar(controller);

    return HookResult.Continue;
  }

  private HookResult OnBombAbortDefuse(EventBombAbortdefuse @event)
  {
    var player = @event.UserIdPlayer;
    if (player == null || !player.IsValid || player.IsFakeClient) return HookResult.Continue;

    var controller = player.Controller as CCSPlayerController;
    if (controller == null || !controller.IsValid) return HookResult.Continue;

    _defuseTokenByControllerIndex.Remove(controller.Index);
    ResetProgressBar(controller);

    return HookResult.Continue;
  }

  private CPlantedC4? GetPlantedC4()
  {
    try
    {
      foreach (var bomb in Core.EntitySystem.GetAllEntitiesByDesignerName<CPlantedC4>("planted_c4"))
      {
        if (bomb != null)
          return bomb;
      }
    }
    catch
    {
    }

    return null;
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

  private static void TrySetMember(object target, string name, object value)
  {
    try
    {
      var type = target.GetType();

      var prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
      if (prop != null && prop.CanWrite)
      {
        prop.SetValue(target, ChangeType(value, prop.PropertyType));
        TryInvokeUpdated(target, name + "Updated");
        return;
      }

      var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
      if (field != null)
      {
        field.SetValue(target, ChangeType(value, field.FieldType));
        TryInvokeUpdated(target, name + "Updated");
      }
    }
    catch
    {
    }
  }

  private static object ChangeType(object value, Type targetType)
  {
    try
    {
      if (targetType.IsInstanceOfType(value)) return value;
      return Convert.ChangeType(value, targetType);
    }
    catch
    {
      return value;
    }
  }

  private static void TryInvokeUpdated(object target, string methodName)
  {
    try
    {
      var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
      method?.Invoke(target, null);
    }
    catch
    {
    }
  }
}

public class FastDefuseConfig
{
  public float Multiplier { get; set; } = 0.5f;
  public float Seconds { get; set; } = 0.0f;
}