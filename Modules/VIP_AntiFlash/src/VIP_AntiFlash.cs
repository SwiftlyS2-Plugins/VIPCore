using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using VIPCore.Contract;
using Microsoft.Extensions.Logging;

namespace VIP_AntiFlash;

[PluginMetadata(Id = "VIP_AntiFlash", Version = "1.0.1-teamfix", Name = "[VIP] AntiFlash", Author = "aga", Description = "Fixed AntiFlash mode 1: immunity applies only to flashbangs thrown by other teammates; self and enemy flashes still blind the player.")]
public class VIP_AntiFlash : BasePlugin
{
    private const string FeatureKey = "vip.antiflash";

    private IVipCoreApiV1? _vipApi;
    private bool _isFeatureRegistered;
    private Guid? _blindHook;
    private bool _configurationErrorLogged;

    public VIP_AntiFlash(ISwiftlyCore core) : base(core)
    {
    }

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
    {
    }

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        DetachVipApi();

        try
        {
            var api = interfaceManager.GetSharedInterface<IVipCoreApiV1>("VIPCore.Api.v1");
            if (api != null)
            {
                _vipApi = api;

                var isCoreReady = _vipApi.IsCoreReady();

                if (isCoreReady)
                    RegisterVipFeatures();
                else
                    _vipApi.OnCoreReady += RegisterVipFeatures;
            }
        }
        catch (Exception ex)
        {
            Core.Logger.LogWarning(ex, "[VIP_AntiFlash] Could not attach VIPCore.Api.v1.");
        }
    }

    public override void Load(bool hotReload)
    {
        _blindHook = Core.GameEvent.HookPre<EventPlayerBlind>(OnPlayerBlind);
    }

    private HookResult OnPlayerBlind(EventPlayerBlind @event)
    {
        if (_vipApi == null) return HookResult.Continue;

        var player = @event.Accessor.GetPlayer("userid");
        if (player == null || !player.IsValid || player.IsFakeClient) return HookResult.Continue;

        if (!_vipApi.IsClientVip(player)) return HookResult.Continue;
        if (_vipApi.GetPlayerFeatureState(player, FeatureKey) != FeatureState.Enabled) return HookResult.Continue;

        var controller = player.Controller;
        if (controller == null) return HookResult.Continue;

        var pawn = controller.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid) return HookResult.Continue;

        int mode;
        try
        {
            // VIPCore's generic API requires a reference type. The converter lets
            // its ConfigurationBinder read the existing scalar "vip.antiflash": 1.
            mode = _vipApi.GetFeatureValue<AntiFlashSettings>(player, FeatureKey)?.Mode ?? 0;
        }
        catch (Exception ex)
        {
            // Bad config must not silently turn into full flash immunity.
            if (!_configurationErrorLogged)
            {
                Core.Logger.LogWarning(ex, "[VIP_AntiFlash] Invalid vip.antiflash setting. Expected integer 0..3. Immunity skipped; check VIP group config.");
                _configurationErrorLogged = true;
            }
            return HookResult.Continue;
        }

        var attacker = @event.Accessor.GetPlayer("attacker");
        var validAttacker = attacker != null && attacker.IsValid;
        var isSelf = validAttacker && player.Slot == attacker!.Slot;
        var sameTeam = validAttacker && attacker!.Controller != null
            && attacker.Controller.Team == controller.Team;

        if (AntiFlashPolicy.ShouldBlock(mode, isSelf, sameTeam))
            pawn.FlashDuration = 0.0f;

        return HookResult.Continue;
    }

    private void RegisterVipFeatures()
    {
        if (_vipApi == null || _isFeatureRegistered) return;

        _vipApi.RegisterFeature(FeatureKey, FeatureType.Toggle, null,
        displayNameResolver: p => Core.Translation.GetPlayerLocalizer(p)["vip.antiflash"]);

        _isFeatureRegistered = true;
        _vipApi.OnCoreReady -= RegisterVipFeatures;
    }

    public override void Unload()
    {
        if (_blindHook is { } hook)
        {
            Core.GameEvent.Unhook(hook);
            _blindHook = null;
        }
        DetachVipApi();
    }

    private void DetachVipApi()
    {
        if (_vipApi != null)
        {
            _vipApi.OnCoreReady -= RegisterVipFeatures;
            if (_isFeatureRegistered)
                _vipApi.UnregisterFeature(FeatureKey);
        }
        _vipApi = null;
        _isFeatureRegistered = false;
    }
}
