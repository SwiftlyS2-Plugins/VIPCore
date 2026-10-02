using System.Globalization;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameHooks;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using VIPCore.Contract;

namespace VIP_Endurance;

[PluginMetadata(Id = "VIP_Endurance", Version = "1.2.0", Name = "[VIP] Endurance",
    Author = "Pisex / SwiftlyS2 port", Description = "Removes VIP hit slowdown at damage, movement and post-frame stages.")]
public sealed class VIP_Endurance(ISwiftlyCore core) : BasePlugin(core)
{
    private const string Original = "Endurance";
    private const string Legacy = "vip.endurance";
    private IVipCoreApiV1? _api;
    private bool _loaded, _registered;
    private Guid _diagnosticCommand;
    private long _ticks, _damageCalls, _movementCalls, _commandCalls, _thinkCalls;
    private readonly Dictionary<ulong, ResetStats> _stats = new();

    private sealed class ResetStats
    {
        public long Count;
        public string LastSource = "none";
        public float LastValue;
    }

    public override void ConfigureSharedInterface(IInterfaceManager manager) { }
    public override void UseSharedInterface(IInterfaceManager manager)
    {
        DetachApi();
        if (!manager.HasSharedInterface("VIPCore.Api.v1"))
        {
            Core.Logger.LogError("[VIP_Endurance] VIPCore.Api.v1 unavailable: Endurance cannot operate.");
            return;
        }
        _api = manager.GetSharedInterface<IVipCoreApiV1>("VIPCore.Api.v1");
        _api.OnCoreReady += Register;
        _api.PlayerRemoved += OnPlayerRemoved;
        if (_loaded && _api.IsCoreReady()) Register();
    }

    public override void Load(bool hotReload)
    {
        // No cached IPlayer objects or per-slot access booleans: connection/spawn/group changes
        // are checked against the current pawn and live VIPCore state at each correction.
        Core.Event.OnTick += OnTick;
        Core.Event.OnClientDisconnected += OnDisconnected;
        Core.GameHooks.Controller.ProcessUsercmds.Pre += BeforeUserCommands;
        Core.GameHooks.Movement.ProcessMovement.Pre += BeforeMovement;
        Core.GameHooks.Entities.TakeDamage.Post += AfterDamage;
        Core.GameHooks.Pawn.PostThink.Post += AfterPawnThink;
        _diagnosticCommand = Core.Command.RegisterCommand("endurancecheck", Diagnostic);
        _loaded = true;
        if (_api?.IsCoreReady() == true) Register();
        Core.Logger.LogInformation("[VIP_Endurance] v1.2.0 loaded: live VIP state, damage-post, movement-pre, usercmds-pre, pawn-post and frame-post correction.");
    }

    private void Register()
    {
        if (!_loaded || _registered || _api?.IsCoreReady() != true) return;
        _api.RegisterFeature(Original, FeatureType.Toggle, OnToggle, DisplayName);
        _api.RegisterFeature(Legacy, FeatureType.Toggle, OnToggle, DisplayName);
        _registered = true;
        Core.Logger.LogInformation("[VIP_Endurance] Registered Endurance and compatibility key vip.endurance. Configure ONE under Groups > group > Values.");
    }

    private string DisplayName(IPlayer p) => Core.Translation.GetPlayerLocalizer(p)["vip.endurance"];
    private void OnToggle(IPlayer p, FeatureState state) => Reset(p, "toggle");

    private bool HasEndurance(IPlayer p)
    {
        if (!_loaded || !_registered || _api == null || !_api.IsCoreReady()) return false;
        return EndurancePolicy.Enabled(true, _api.IsClientVip(p),
            _api.GetPlayerFeatureState(p, Original), _api.GetPlayerFeatureState(p, Legacy));
    }

    private void Reset(IPlayer p, string source)
    {
        if (!_loaded || !p.IsValid || p.IsFakeClient || !p.IsAlive) return;
        var pawn = p.PlayerPawn;
        if (pawn is not { IsValid: true } || pawn.Health <= 0) return;
        var value = pawn.VelocityModifier;
        if (!EndurancePolicy.NeedsReset(value) || !HasEndurance(p)) return;
        // Match the original field and threshold exactly. Do not modify damage, HP,
        // stamina, velocity, movement type, weapon speed, flinch animations or server cvars.
        pawn.VelocityModifier = 1.0f;
        pawn.VelocityModifierUpdated();
        if (!_stats.TryGetValue(p.SessionId, out var stats)) _stats[p.SessionId] = stats = new();
        stats.Count++;
        stats.LastValue = value;
        stats.LastSource = source;
    }

    private void OnTick()
    {
        _ticks++;
        if (!_loaded || !_registered) return;
        foreach (var p in Core.PlayerManager.GetAllPlayers()) Reset(p, "frame-post");
    }
    private void BeforeUserCommands(ref ProcessUsercmdsPreContext ctx)
    {
        _commandCalls++;
        Reset(ctx.Params.Player, "usercmds-pre");
    }
    private void BeforeMovement(ref ProcessMovementMovementPreContext ctx)
    {
        _movementCalls++;
        Reset(ctx.Params.Player, "movement-pre");
    }
    private void AfterPawnThink(ref PostThinkPawnPostContext ctx)
    {
        _thinkCalls++;
        Reset(ctx.Params.Player, "pawn-post");
    }
    private void AfterDamage(ref TakeDamageEntityPostContext ctx)
    {
        _damageCalls++;
        var entity = ctx.Params.Entity;
        if (!_loaded || !entity.IsValid) return;
        // Match the damaged entity to a real live player's pawn. Never cast arbitrary
        // props/grenades to a pawn and never inspect an attacker's access by mistake.
        foreach (var p in Core.PlayerManager.GetAllPlayers())
        {
            if (!p.IsValid || p.IsFakeClient) continue;
            var pawn = p.PlayerPawn;
            if (pawn is not { IsValid: true } || pawn.Index != entity.Index) continue;
            Reset(p, "damage-post");
            break;
        }
    }

    private void OnPlayerRemoved(IPlayer p, string group) => _stats.Remove(p.SessionId);
    private void OnDisconnected(IOnClientDisconnectedEvent e)
    {
        var p = Core.PlayerManager.GetPlayer(e.PlayerId);
        if (p != null) _stats.Remove(p.SessionId);
    }

    private void Diagnostic(ICommandContext ctx)
    {
        if (ctx.Sender is not { IsValid: true } p)
        {
            ctx.Reply($"VIP_Endurance 1.2.0: API={_api != null}, Ready={_api?.IsCoreReady() == true}, Registered={_registered}; hooks ticks={_ticks}, damage={_damageCalls}, movement={_movementCalls}, cmds={_commandCalls}, think={_thinkCalls}. Run !endurancecheck as the player for access details.");
            return;
        }
        var ready = _api?.IsCoreReady() == true;
        var vip = ready && _api!.IsClientVip(p);
        var original = ready ? _api!.GetPlayerFeatureState(p, Original) : FeatureState.NoAccess;
        var legacy = ready ? _api!.GetPlayerFeatureState(p, Legacy) : FeatureState.NoAccess;
        var group = vip ? _api!.GetClientVipGroup(p) : "none";
        var pawn = p.PlayerPawn;
        var vm = pawn is { IsValid: true } ? pawn.VelocityModifier.ToString("0.000", CultureInfo.InvariantCulture) : "no pawn";
        var enabled = _registered && EndurancePolicy.Enabled(ready, vip, original, legacy);
        _stats.TryGetValue(p.SessionId, out var stats);
        var lines = new[]
        {
            $"Endurance 1.2.0: API={_api != null}; Ready={ready}; Registered={_registered}; VIP={vip}; Group={group}",
            $"Endurance={original}; vip.endurance={legacy}; Effective={enabled}; VelocityModifier={vm}",
            $"Your resets={stats?.Count ?? 0}; last={stats?.LastSource ?? "none"}; last VM={stats?.LastValue.ToString("0.000", CultureInfo.InvariantCulture) ?? "n/a"}",
            $"Hooks: ticks={_ticks}; damage={_damageCalls}; movement={_movementCalls}; cmds={_commandCalls}; think={_thinkCalls}"
        };
        foreach (var line in lines)
        {
            p.SendMessage(MessageType.Chat, line);
            p.SendMessage(MessageType.Console, line);
        }
        if (!enabled)
            p.SendMessage(MessageType.Chat, "Enable Endurance in the VIP menu and check vip_groups > Groups > your group > Values > Endurance. Features is the wrong section.");
    }

    private void DetachApi()
    {
        if (_api == null) return;
        _api.OnCoreReady -= Register;
        _api.PlayerRemoved -= OnPlayerRemoved;
        if (_registered) { _api.UnregisterFeature(Original); _api.UnregisterFeature(Legacy); }
        _registered = false;
        _api = null;
    }
    public override void Unload()
    {
        _loaded = false;
        Core.Event.OnTick -= OnTick;
        Core.Event.OnClientDisconnected -= OnDisconnected;
        Core.GameHooks.Controller.ProcessUsercmds.Pre -= BeforeUserCommands;
        Core.GameHooks.Movement.ProcessMovement.Pre -= BeforeMovement;
        Core.GameHooks.Entities.TakeDamage.Post -= AfterDamage;
        Core.GameHooks.Pawn.PostThink.Post -= AfterPawnThink;
        Core.Command.UnregisterCommand(_diagnosticCommand);
        _stats.Clear();
        DetachApi();
    }
}
