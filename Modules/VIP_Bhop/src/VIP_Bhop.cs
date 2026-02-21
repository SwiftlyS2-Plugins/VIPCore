using System.Text.Json.Serialization;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Convars;
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

    private IConVar<bool>? _autobunnyhopping;
    private IConVar<bool>? _enablebunnyhopping;

    private ConvarFlags? _autobunnyhoppingOriginalFlags;
    private ConvarFlags? _enablebunnyhoppingOriginalFlags;

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

        _autobunnyhopping = Core.ConVar.Find<bool>("sv_autobunnyhopping");
        _enablebunnyhopping = Core.ConVar.Find<bool>("sv_enablebunnyhopping");

        if (_autobunnyhopping != null)
            _autobunnyhoppingOriginalFlags = _autobunnyhopping.Flags;
        if (_enablebunnyhopping != null)
            _enablebunnyhoppingOriginalFlags = _enablebunnyhopping.Flags;

        UpdateGlobalBhopState();

        Core.Event.OnClientConnected += OnClientConnected;
        Core.Event.OnClientDisconnected += OnClientDisconnected;
        Core.Event.OnTick += OnTick;
        Core.GameEvent.HookPre<EventRoundFreezeEnd>(OnRoundFreezeEnd);
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
        UpdateGlobalBhopState();
    }

    private void OnClientDisconnected(IOnClientDisconnectedEvent args)
    {
        if (args.PlayerId < 0 || args.PlayerId >= _bhopSettings.Length) return;
        _bhopSettings[args.PlayerId] = new BhopSettings();
        UpdateGlobalBhopState();
    }

    private void OnTick()
    {
        if (_autobunnyhopping == null || _enablebunnyhopping == null) return;

        foreach (var player in Core.PlayerManager.GetAllPlayers())
        {
            if (player == null || !player.IsValid || player.IsFakeClient) continue;
            if (player.PlayerID < 0 || player.PlayerID >= _bhopSettings.Length) continue;

            var settings = _bhopSettings[player.PlayerID];
            var entitled = settings.Active && settings.Enabled;

            SetBunnyhop(player, entitled);

            if (entitled && player.IsAlive)
                ClampPlayerSpeed(player, settings.MaxSpeed);
        }
    }

    private static void ClampPlayerSpeed(IPlayer player, float maxSpeed)
    {
        if (maxSpeed <= 0) return;

        var pawn = player.PlayerPawn;
        if (pawn == null || !pawn.IsValid) return;

        var velocity = pawn.AbsVelocity;
        var horizontalSpeed = Math.Sqrt((velocity.X * velocity.X) + (velocity.Y * velocity.Y));
        if (horizontalSpeed <= maxSpeed || horizontalSpeed <= 0.001f) return;

        var ratio = (float)(maxSpeed / horizontalSpeed);
        pawn.AbsVelocity.X *= ratio;
        pawn.AbsVelocity.Y *= ratio;
        pawn.VelocityUpdated();
    }

    private void UpdateGlobalBhopState()
    {
        if (_autobunnyhopping == null || _enablebunnyhopping == null) return;

        var shouldEnable = false;
        for (var i = 0; i < _bhopSettings.Length; i++)
        {
            var settings = _bhopSettings[i];
            if (settings.Active && settings.Enabled)
            {
                shouldEnable = true;
                break;
            }
        }

        if (shouldEnable)
        {
            if ((_autobunnyhopping.Flags & ConvarFlags.CHEAT) != 0)
                _autobunnyhopping.Flags = _autobunnyhopping.Flags & ~ConvarFlags.CHEAT;
            if ((_enablebunnyhopping.Flags & ConvarFlags.CHEAT) != 0)
                _enablebunnyhopping.Flags = _enablebunnyhopping.Flags & ~ConvarFlags.CHEAT;

            if (!_autobunnyhopping.Value)
                _autobunnyhopping.SetInternal(true);
            if (!_enablebunnyhopping.Value)
                _enablebunnyhopping.SetInternal(true);
        }
        else
        {
            if (_autobunnyhopping.Value)
                _autobunnyhopping.SetInternal(false);
            if (_enablebunnyhopping.Value)
                _enablebunnyhopping.SetInternal(false);

            if (_autobunnyhoppingOriginalFlags.HasValue)
                _autobunnyhopping.Flags = _autobunnyhoppingOriginalFlags.Value;
            if (_enablebunnyhoppingOriginalFlags.HasValue)
                _enablebunnyhopping.Flags = _enablebunnyhoppingOriginalFlags.Value;
        }
    }

    private HookResult OnRoundFreezeEnd(EventRoundFreezeEnd @event)
    {
        var players = Core.PlayerManager.GetAllPlayers();
        foreach (var player in players)
        {
            if (player == null || player.IsFakeClient || !player.IsValid) continue;
            EnableBhopForPlayer(player);
        }

        return HookResult.Continue;
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
        if (_vipApi == null)
            return;

        if (player.PlayerID < 0 || player.PlayerID >= _bhopSettings.Length) return;

        var settings = _bhopSettings[player.PlayerID];
        
        // If they already have active bhop, no need to do anything
        if (settings.Active) return;

        settings.Active = false;
        UpdateGlobalBhopState();

        if (!_vipApi.IsClientVip(player))
            return;

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
                UpdateGlobalBhopState();
                SetBunnyhop(player, true);
            });
        }
        else
        {
            // Activate immediately if timer is 0
            settings.Active = true;
            UpdateGlobalBhopState();
            SetBunnyhop(player, true);
        }
    }

    private void SetBunnyhop(IPlayer player, bool value)
    {
        if (player.PlayerID < 0 || player.PlayerID >= _bhopSettings.Length) return;

        _enablebunnyhopping?.ReplicateToClient(player.PlayerID, value);
        _autobunnyhopping?.ReplicateToClient(player.PlayerID, value);
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
                UpdateGlobalBhopState();
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
        };
    }

    public override void Unload()
    {
        Core.Event.OnClientConnected -= OnClientConnected;
        Core.Event.OnClientDisconnected -= OnClientDisconnected;
        Core.Event.OnTick -= OnTick;

        if (_vipApi != null)
        {
            _vipApi.OnCoreReady -= RegisterVipFeatures;
            if (_isFeatureRegistered)
                _vipApi.UnregisterFeature(FeatureKey);
        }

        if (_autobunnyhopping != null)
            _autobunnyhopping.SetInternal(false);
        if (_enablebunnyhopping != null)
            _enablebunnyhopping.SetInternal(false);

        if (_autobunnyhopping != null && _autobunnyhoppingOriginalFlags.HasValue)
            _autobunnyhopping.Flags = _autobunnyhoppingOriginalFlags.Value;
        if (_enablebunnyhopping != null && _enablebunnyhoppingOriginalFlags.HasValue)
            _enablebunnyhopping.Flags = _enablebunnyhoppingOriginalFlags.Value;
    }
}

public class BhopSettings
{
    [JsonIgnore] public bool Active { get; set; }
    [JsonIgnore] public bool Enabled { get; set; }
    public float Timer { get; set; } = 5.0f;
    public float MaxSpeed { get; set; } = 300.0f;
}

/// <summary>
/// Configurable values read from the VIP group config.
/// Any properties added here will be automatically mapped from the group config JSON.
/// </summary>
public class BhopConfig
{
    public float Timer { get; set; } = 5.0f;
    public float MaxSpeed { get; set; } = 300.0f;
}