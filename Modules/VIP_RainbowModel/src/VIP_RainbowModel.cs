using Microsoft.Extensions.DependencyInjection;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Misc;
using VIPCore.Contract;

namespace VIP_RainbowModel;

[PluginMetadata(Id = "VIP_RainbowModel", Version = "1.0.0", Name = "VIP_RainbowModel", Author = "aga", Description = "No description.")]
public partial class VIP_RainbowModel : BasePlugin
{
  private const string FeatureKey = "vip.rainbowmodel";
  private const float DefaultIntervalSeconds = 1.4f;

  private IVipCoreApiV1? _vipApi;
  private bool _isFeatureRegistered;

  private readonly int[] _generationBySlot = new int[70];

  public VIP_RainbowModel(ISwiftlyCore core) : base(core)
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
    Core.Event.OnClientConnected += OnClientConnected;
    Core.Event.OnClientDisconnected += OnClientDisconnected;

    Core.GameEvent.HookPost<EventPlayerSpawn>(OnPlayerSpawn);
    Core.GameEvent.HookPost<EventPlayerDeath>(OnPlayerDeath);

    RegisterVipFeaturesWhenReady();
  }

  public override void Unload()
  {
    Core.Event.OnClientConnected -= OnClientConnected;
    Core.Event.OnClientDisconnected -= OnClientDisconnected;

    if (_vipApi != null)
    {
      _vipApi.OnCoreReady -= RegisterVipFeatures;
      _vipApi.OnPlayerSpawn -= OnVipPlayerSpawn;

      if (_isFeatureRegistered)
        _vipApi.UnregisterFeature(FeatureKey);
    }

    for (var i = 0; i < _generationBySlot.Length; i++)
      _generationBySlot[i]++;
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
      OnSelectItem,
      displayNameResolver: _ => "Rainbow Model"
    );

    _isFeatureRegistered = true;
    _vipApi.OnPlayerSpawn += OnVipPlayerSpawn;
  }

  private void OnClientConnected(IOnClientConnectedEvent @event)
  {
    if (@event.PlayerId < 0 || @event.PlayerId >= _generationBySlot.Length) return;
    _generationBySlot[@event.PlayerId]++;
  }

  private void OnClientDisconnected(IOnClientDisconnectedEvent @event)
  {
    if (@event.PlayerId < 0 || @event.PlayerId >= _generationBySlot.Length) return;
    _generationBySlot[@event.PlayerId]++;
  }

  private void OnVipPlayerSpawn(IPlayer player)
  {
    TryStartForPlayer(player);
  }

  private HookResult OnPlayerSpawn(EventPlayerSpawn @event)
  {
    TryStartForPlayer(@event.UserIdPlayer);
    return HookResult.Continue;
  }

  private HookResult OnPlayerDeath(EventPlayerDeath @event)
  {
    var victim = @event.Accessor.GetPlayer("userid");
    if (victim == null || !victim.IsValid) return HookResult.Continue;

    StopForPlayer(victim);
    return HookResult.Continue;
  }

  private void OnSelectItem(IPlayer player, FeatureState state)
  {
    if (state == FeatureState.Disabled)
    {
      StopForPlayer(player);
      return;
    }

    TryStartForPlayer(player);
  }

  private void TryStartForPlayer(IPlayer? player)
  {
    if (_vipApi == null) return;
    if (player == null || !player.IsValid || player.IsFakeClient) return;

    if (!_vipApi.IsClientVip(player)) return;
    if (_vipApi.GetPlayerFeatureState(player, FeatureKey) != FeatureState.Enabled) return;

    var enabled = true;
    var interval = DefaultIntervalSeconds;
    try
    {
      var cfg = _vipApi.GetFeatureValue<RainbowModelConfig>(player, FeatureKey);
      if (cfg != null)
      {
        enabled = cfg.Enabled;
        if (cfg.IntervalSeconds > 0.0f)
          interval = cfg.IntervalSeconds;
      }
    }
    catch
    {
      enabled = true;
      interval = DefaultIntervalSeconds;
    }

    if (!enabled)
      return;

    var controller = player.Controller as CCSPlayerController;
    if (controller == null || !controller.IsValid) return;

    var pawn = controller.PlayerPawn.Value;
    if (pawn == null || !pawn.IsValid) return;

    StartLoop(player.Slot, pawn, interval);
  }

  private void StopForPlayer(IPlayer? player)
  {
    if (player == null || !player.IsValid) return;
    if (player.Slot < 0 || player.Slot >= _generationBySlot.Length) return;

    _generationBySlot[player.Slot]++;

    var controller = player.Controller as CCSPlayerController;
    if (controller == null || !controller.IsValid) return;

    var pawn = controller.PlayerPawn.Value;
    if (pawn == null || !pawn.IsValid) return;

    SetPawnRender(pawn, 255, 255, 255);
  }

  private void StartLoop(int slot, CCSPlayerPawn pawn, float interval)
  {
    if (slot < 0 || slot >= _generationBySlot.Length) return;

    _generationBySlot[slot]++;
    var gen = _generationBySlot[slot];

    Tick(slot, gen, pawn, interval);
  }

  private void Tick(int slot, int gen, CCSPlayerPawn pawn, float interval)
  {
    if (slot < 0 || slot >= _generationBySlot.Length) return;
    if (_generationBySlot[slot] != gen) return;
    if (pawn == null || !pawn.IsValid) return;

    SetPawnRender(pawn, Random.Shared.Next(0, 256), Random.Shared.Next(0, 256), Random.Shared.Next(0, 256));

    Core.Scheduler.DelayBySeconds(interval, () => Tick(slot, gen, pawn, interval));
  }

  private static void SetPawnRender(CCSPlayerPawn pawn, int r, int g, int b)
  {
    pawn.Render = new Color(r, g, b, 255);
    pawn.RenderUpdated();
  }
}

public class RainbowModelConfig
{
  public bool Enabled { get; set; } = true;
  public float IntervalSeconds { get; set; } = 1.4f;
}