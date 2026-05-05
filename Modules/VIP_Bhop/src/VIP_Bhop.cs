using System.Text.Json.Serialization;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Natives;
using VIPCore.Contract;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace VIP_Bhop;

[PluginMetadata(Id = "VIP_Bhop", Version = "1.0.0", Name = "[VIP] Bhop", Author = "aga")]
public class VIP_Bhop : BasePlugin
{
    private const string FeatureKey = "vip.bhop";
    private IVipCoreApiV1? _vipApi;
    private bool _isFeatureRegistered;
    private readonly BhopSettings[] _bhopSettings = new BhopSettings[65];

    public VIP_Bhop(ISwiftlyCore core) : base(core)
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
        for (int i = 0; i < _bhopSettings.Length; i++)
        {
            _bhopSettings[i] = new BhopSettings();
        }

        Core.Event.OnClientConnected += OnClientConnected;
        Core.Event.OnClientDisconnected += OnClientDisconnected;
        Core.Event.OnClientKeyStateChanged += OnClientKeyStateChanged;
        Core.Event.OnPlayerPawnPostThink += OnPlayerPawnPostThink;
        Core.GameEvent.HookPre<EventPlayerSpawn>(OnPlayerSpawn);

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

    private void OnClientConnected(IOnClientConnectedEvent args)
    {
        if (args.PlayerId < 0 || args.PlayerId >= _bhopSettings.Length) return;
        _bhopSettings[args.PlayerId] = new BhopSettings();
    }

    private void OnClientDisconnected(IOnClientDisconnectedEvent args)
    {
        if (args.PlayerId < 0 || args.PlayerId >= _bhopSettings.Length) return;
        _bhopSettings[args.PlayerId] = new BhopSettings();
    }

    private void OnClientKeyStateChanged(IOnClientKeyStateChangedEvent @event)
    {
        if (@event.Key != KeyKind.Space) return;
        if (@event.PlayerId < 0 || @event.PlayerId >= _bhopSettings.Length) return;
        _bhopSettings[@event.PlayerId].IsHoldingJump = @event.Pressed;
    }

    private void OnPlayerPawnPostThink(IOnPlayerPawnPostThinkHookEvent @event)
    {
        var csPawn = @event.PlayerPawn;
        CBasePlayerPawn pawn = csPawn;

        var player = pawn.ToPlayer();
        if (player == null || player.IsFakeClient) return;

        var playerId = player.PlayerID;
        if (playerId < 0 || playerId >= _bhopSettings.Length) return;

        var settings = _bhopSettings[playerId];
        if (!settings.Active || !settings.Enabled) return;
        if (!player.IsAlive) return;

        var isGrounded = (pawn.Flags & 1u) != 0;
        var wasGrounded = settings.PrevGrounded;
        var velocity = pawn.AbsVelocity;

        // Track horizontal velocity while airborne for restoration on landing
        if (!isGrounded)
        {
            settings.PreLandVelocityX = velocity.X;
            settings.PreLandVelocityY = velocity.Y;
        }

        // Auto-bhop: just landed while holding jump — restore pre-landing velocity and re-jump
        if (!wasGrounded && isGrounded && settings.IsHoldingJump)
        {
            csPawn.Teleport(null, null, new Vector(
                settings.PreLandVelocityX,
                settings.PreLandVelocityY,
                settings.JumpForce));
        }
        // Speed cap while airborne
        else if (!isGrounded && settings.MaxSpeed > 0)
        {
            ClampPlayerSpeed(csPawn, settings.MaxSpeed, velocity);
        }

        settings.PrevGrounded = isGrounded;
    }

    private static void ClampPlayerSpeed(CCSPlayerPawn pawn, float maxSpeed, Vector velocity)
    {
        var vx = velocity.X;
        var vy = velocity.Y;
        var horizontalSpeedSqr = (vx * vx) + (vy * vy);
        var maxSpeedSqr = maxSpeed * maxSpeed;

        if (horizontalSpeedSqr <= maxSpeedSqr || horizontalSpeedSqr <= 0.001f) return;

        var horizontalSpeed = Math.Sqrt(horizontalSpeedSqr);
        var ratio = (float)(maxSpeed / horizontalSpeed);

        pawn.Teleport(null, null, new Vector(vx * ratio, vy * ratio, velocity.Z));
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event)
    {
        var player = @event.UserIdPlayer;
        if (player == null || player.IsFakeClient || !player.IsValid) return HookResult.Continue;

        // Give a slight delay to allow the player to fully spawn
        Core.Scheduler.DelayBySeconds(0.1f, () =>
        {
            EnableBhopForPlayer(player);
        });

        return HookResult.Continue;
    }

    private void EnableBhopForPlayer(IPlayer player)
    {
        if (_vipApi == null) return;

        if (player.PlayerID < 0 || player.PlayerID >= _bhopSettings.Length) return;

        var settings = _bhopSettings[player.PlayerID];

        settings.Active = false;
        settings.PrevGrounded = true;
        settings.IsHoldingJump = false;

        if (!_vipApi.IsClientVip(player)) return;

        var featureState = _vipApi.GetPlayerFeatureState(player, FeatureKey);

        settings.Enabled = featureState == FeatureState.Enabled;
        if (!settings.Enabled) return;

        var timer = settings.Timer;

        if (timer > 0)
        {
            Core.Scheduler.NextTick(() =>
            {
                var localizer = Core.Translation.GetPlayerLocalizer(player);
                player.SendMessage(MessageType.Chat, localizer["bhop.TimeToActivation", timer]);
            });

            Core.Scheduler.DelayBySeconds(timer, () =>
            {
                Core.Scheduler.NextTick(() =>
                {
                    var localizer = Core.Translation.GetPlayerLocalizer(player);
                    player.SendMessage(MessageType.Chat, localizer["bhop.Activated"]);
                });
                settings.Active = true;
            });
        }
        else
        {
            settings.Active = true;
        }
    }

    private void RegisterVipFeatures()
    {
        if (_vipApi == null || _isFeatureRegistered) return;

        _vipApi.RegisterFeature(FeatureKey, FeatureType.Toggle, (player, state) =>
        {
            Core.Scheduler.NextTick(() =>
            {
                if (player.PlayerID < 0 || player.PlayerID >= _bhopSettings.Length) return;
                _bhopSettings[player.PlayerID].Enabled = state == FeatureState.Enabled;
            });
        },
        displayNameResolver: p => Core.Translation.GetPlayerLocalizer(p)["vip.bhop"]);

        _isFeatureRegistered = true;

        _vipApi.PlayerLoaded += (player, group) =>
        {
            if (_vipApi == null) return;

            if (player.PlayerID < 0 || player.PlayerID >= _bhopSettings.Length) return;

            var state = _vipApi.GetPlayerFeatureState(player, FeatureKey);
            _bhopSettings[player.PlayerID].Enabled = state == FeatureState.Enabled;

            var config = _vipApi.GetFeatureValue<BhopConfig>(player, FeatureKey);
            _bhopSettings[player.PlayerID].Timer = config?.Timer ?? 5.0f;
            _bhopSettings[player.PlayerID].MaxSpeed = config?.MaxSpeed ?? 300.0f;
            _bhopSettings[player.PlayerID].JumpForce = config?.JumpForce ?? 305.0f;
        };
    }

    public override void Unload()
    {
        Core.Event.OnClientConnected -= OnClientConnected;
        Core.Event.OnClientDisconnected -= OnClientDisconnected;
        Core.Event.OnClientKeyStateChanged -= OnClientKeyStateChanged;
        Core.Event.OnPlayerPawnPostThink -= OnPlayerPawnPostThink;

        if (_vipApi != null)
        {
            _vipApi.OnCoreReady -= RegisterVipFeatures;
            if (_isFeatureRegistered)
                _vipApi.UnregisterFeature(FeatureKey);
        }
    }
}

public class BhopSettings
{
    [JsonIgnore] public bool Active { get; set; }
    [JsonIgnore] public bool Enabled { get; set; }
    [JsonIgnore] public bool IsHoldingJump { get; set; }
    [JsonIgnore] public bool PrevGrounded { get; set; } = true;
    [JsonIgnore] public float PreLandVelocityX { get; set; }
    [JsonIgnore] public float PreLandVelocityY { get; set; }
    public float Timer { get; set; } = 5.0f;
    public float MaxSpeed { get; set; } = 300.0f;
    public float JumpForce { get; set; } = 305.0f;
}

/// <summary>
/// Configurable values read from the VIP group config.
/// Any properties added here will be automatically mapped from the group config JSON.
/// </summary>
public class BhopConfig
{
    public float Timer { get; set; } = 5.0f;
    public float MaxSpeed { get; set; } = 300.0f;
    public float JumpForce { get; set; } = 305.0f;
}