using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using VIPCore.Contract;

namespace VIP_AntiFlash;

[PluginMetadata(Id = "VIP_AntiFlash", Version = "1.0.0", Name = "[VIP] AntiFlash", Author = "aga", Description = "No description.")]
public class VIP_AntiFlash : BasePlugin
{
    private const string FeatureKey = "vip.antiflash";

    private IVipCoreApiV1? _vipApi;
    private bool _isFeatureRegistered;

    public VIP_AntiFlash(ISwiftlyCore core) : base(core)
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
        catch
        {
        }
    }

    public override void Load(bool hotReload)
    {
        Core.GameEvent.HookPre<EventPlayerBlind>(OnPlayerBlind);
    }

    private HookResult OnPlayerBlind(EventPlayerBlind @event)
    {
        if (_vipApi == null) return HookResult.Continue;

        var player = @event.Accessor.GetPlayer("userid");
        if (player == null || player.IsFakeClient) return HookResult.Continue;

        if (!_vipApi.IsClientVip(player)) return HookResult.Continue;
        if (_vipApi.GetPlayerFeatureState(player, FeatureKey) != FeatureState.Enabled) return HookResult.Continue;

        var controller = player.Controller;
        if (controller == null) return HookResult.Continue;

        var pawn = controller.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid) return HookResult.Continue;

        var featureValue = 0;
        var attacker = @event.Accessor.GetPlayer("attacker");

        var sameTeam = attacker != null && attacker.Controller?.Team == player.Controller?.Team;

        switch (featureValue)
        {
            case 1:
                if (sameTeam && player.Slot != attacker?.Slot)
                    pawn.FlashDuration = 0.0f;
                break;
            case 2:
                if (player.Slot == attacker?.Slot)
                    pawn.FlashDuration = 0.0f;
                break;
            case 3:
                if (sameTeam || player.Slot == attacker?.Slot)
                    pawn.FlashDuration = 0.0f;
                break;
            default:
                pawn.FlashDuration = 0.0f;
                break;
        }

        return HookResult.Continue;
    }

    private void RegisterVipFeatures()
    {
        if (_vipApi == null || _isFeatureRegistered) return;

        _vipApi.RegisterFeature(FeatureKey, FeatureType.Toggle, null,
        displayNameResolver: p => Core.Translation.GetPlayerLocalizer(p)["vip.antiflash"]);

        _isFeatureRegistered = true;

        _vipApi.PlayerLoaded += (player, group) =>
        {
        };
    }

    public override void Unload()
    {
        if (_vipApi == null) return;
        _vipApi.UnregisterFeature(FeatureKey);
    }
}