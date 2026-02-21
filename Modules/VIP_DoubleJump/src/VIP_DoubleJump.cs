using Microsoft.Extensions.DependencyInjection;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using VIPCore.Contract;

namespace VIP_DoubleJump;

[PluginMetadata(Id = "VIP_DoubleJump", Version = "1.0.0", Name = "VIP_DoubleJump", Author = "aga", Description = "No description.")]
public partial class VIP_DoubleJump : BasePlugin {
  private const string FeatureKey = "vip.doublejump";

  private IVipCoreApiV1? _vipApi;
  private bool _isFeatureRegistered;

  private sealed class DoubleJumpConfig
  {
    public int MaxJumps { get; set; } = 2;
    public float Boost { get; set; } = 320.0f;
  }

  private sealed class PlayerState
  {
    public bool Enabled;
    public int MaxJumps = 2;
    public float Boost = 320.0f;

    public int JumpsUsed;
    public bool PrevGrounded;
    public bool PendingJumpPress;
    public bool JumpReleasedSinceGround;
  }

  private readonly PlayerState[] _states = new PlayerState[65];
  private readonly List<IPlayer> _cachedPlayers = new List<IPlayer>(64);
  private bool _playerListDirty = true;

  public VIP_DoubleJump(ISwiftlyCore core) : base(core)
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
    for (var i = 0; i < _states.Length; i++)
    {
      _states[i] = new PlayerState();
    }

    Core.Event.OnClientConnected += OnClientConnected;
    Core.Event.OnClientDisconnected += OnClientDisconnected;
    Core.Event.OnClientKeyStateChanged += OnClientKeyStateChanged;
    Core.Event.OnTick += OnTick;

    RegisterVipFeaturesWhenReady();
  }

  public override void Unload() {
    Core.Event.OnClientConnected -= OnClientConnected;
    Core.Event.OnClientDisconnected -= OnClientDisconnected;
    Core.Event.OnClientKeyStateChanged -= OnClientKeyStateChanged;
    Core.Event.OnTick -= OnTick;

    if (_vipApi != null)
    {
      _vipApi.OnCoreReady -= RegisterVipFeatures;
      _vipApi.PlayerLoaded -= OnPlayerLoaded;
      _vipApi.PlayerRemoved -= OnPlayerRemoved;
      _vipApi.OnPlayerSpawn -= OnPlayerSpawn;
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

    _vipApi.RegisterFeature(FeatureKey, FeatureType.Toggle, (player, state) =>
    {
      Core.Scheduler.NextTick(() =>
      {
        if (player.PlayerID < 0 || player.PlayerID >= _states.Length) return;
        _states[player.PlayerID].Enabled = state == FeatureState.Enabled;
      });
    },
    displayNameResolver: p => "Double Jump");

    _isFeatureRegistered = true;

    _vipApi.PlayerLoaded += OnPlayerLoaded;
    _vipApi.PlayerRemoved += OnPlayerRemoved;
    _vipApi.OnPlayerSpawn += OnPlayerSpawn;
  }

  private void OnPlayerSpawn(IPlayer player)
  {
    if (player.PlayerID < 0 || player.PlayerID >= _states.Length) return;
    var s = _states[player.PlayerID];
    s.JumpsUsed = 0;
    s.PrevGrounded = true;
  }

  private void OnPlayerLoaded(IPlayer player, string group)
  {
    if (_vipApi == null) return;
    if (player.PlayerID < 0 || player.PlayerID >= _states.Length) return;

    var s = _states[player.PlayerID];

    var featureState = _vipApi.GetPlayerFeatureState(player, FeatureKey);
    s.Enabled = featureState == FeatureState.Enabled;

    var cfg = _vipApi.GetFeatureValue<DoubleJumpConfig>(player, FeatureKey);
    s.MaxJumps = cfg?.MaxJumps ?? 2;
    s.Boost = cfg?.Boost ?? 320.0f;

    s.JumpsUsed = 0;
  }

  private void OnPlayerRemoved(IPlayer player, string group)
  {
    if (player.PlayerID < 0 || player.PlayerID >= _states.Length) return;
    _states[player.PlayerID] = new PlayerState();
  }

  private void OnClientConnected(IOnClientConnectedEvent args)
  {
    if (args.PlayerId < 0 || args.PlayerId >= _states.Length) return;
    _states[args.PlayerId] = new PlayerState();
    _playerListDirty = true;
  }

  private void OnClientDisconnected(IOnClientDisconnectedEvent args)
  {
    if (args.PlayerId < 0 || args.PlayerId >= _states.Length) return;
    _states[args.PlayerId] = new PlayerState();
    _playerListDirty = true;
  }

  private void OnClientKeyStateChanged(IOnClientKeyStateChangedEvent @event)
  {
    if (@event.Key != KeyKind.Space) return;
    if (@event.PlayerId < 0 || @event.PlayerId >= _states.Length) return;

    var s = _states[@event.PlayerId];
    if (!@event.Pressed)
    {
      s.JumpReleasedSinceGround = true;
      return;
    }

    s.PendingJumpPress = true;
  }

  private void OnTick()
  {
    if (_playerListDirty)
    {
      _cachedPlayers.Clear();
      _cachedPlayers.AddRange(Core.PlayerManager.GetAllPlayers());
      _playerListDirty = false;
    }

    for (int i = 0; i < _cachedPlayers.Count; i++)
    {
      var player = _cachedPlayers[i];
      if (!player.IsValid || player.IsFakeClient) continue;
      
      var playerId = player.PlayerID;
      if (playerId < 0 || playerId >= _states.Length) continue;

      var s = _states[playerId];
      if (!s.Enabled || s.MaxJumps <= 1) continue;

      var jumpJustPressed = s.PendingJumpPress;
      s.PendingJumpPress = false;

      var pawn = player.Pawn;
      if (pawn is not { IsValid: true }) continue;
      if (pawn.LifeState != (byte)LifeState_t.LIFE_ALIVE) continue;

      var flags = pawn.Flags;
      var isGrounded = (flags & 1u) != 0;
      var wasGrounded = s.PrevGrounded;

      if (isGrounded)
      {
        s.JumpsUsed = 0;
        s.JumpReleasedSinceGround = false;
      }
      else if (wasGrounded)
      {
        if (s.JumpsUsed < 1)
          s.JumpsUsed = 1;
      }

      if (jumpJustPressed
          && !isGrounded
          && !wasGrounded
          && s.JumpsUsed >= 1
          && s.JumpsUsed < s.MaxJumps)
      {
        if (!s.JumpReleasedSinceGround)
          jumpJustPressed = false;
      }

      if (jumpJustPressed
          && !isGrounded
          && !wasGrounded
          && s.JumpsUsed >= 1
          && s.JumpsUsed < s.MaxJumps)
      {
        s.JumpsUsed++;
        pawn.AbsVelocity.Z = s.Boost;
        s.JumpReleasedSinceGround = false;
      }

      s.PrevGrounded = isGrounded;
    }
  }
}