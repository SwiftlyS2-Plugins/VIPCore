using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using VIPCore.Contract;

namespace VIP_AntiFlash;

[PluginMetadata(Id = "VIP_AntiFlash", Version = "1.1.0", Name = "[VIP] AntiFlash", Author = "aga", Description = "No description.")]
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

        var attacker = @event.Accessor.GetPlayer("attacker");

        var sameTeam = attacker != null && attacker.Controller?.Team == player.Controller?.Team;

        switch (ReadMode(player))
        {
            case AntiFlashMode.Teammates:
                if (sameTeam && player.Slot != attacker?.Slot)
                    pawn.FlashDuration = 0.0f;
                break;
            case AntiFlashMode.Own:
                if (player.Slot == attacker?.Slot)
                    pawn.FlashDuration = 0.0f;
                break;
            case AntiFlashMode.TeammatesAndOwn:
                if (sameTeam || player.Slot == attacker?.Slot)
                    pawn.FlashDuration = 0.0f;
                break;
            default:
                pawn.FlashDuration = 0.0f;
                break;
        }

        return HookResult.Continue;
    }

    // A bare value such as "vip.antiflash": 1 binds no Mode, so it keeps full immunity.
    private AntiFlashMode ReadMode(IPlayer player)
    {
        try
        {
            return _vipApi?.GetFeatureValue<AntiFlashConfig>(player, FeatureKey)?.Mode ?? AntiFlashMode.All;
        }
        catch
        {
            return AntiFlashMode.All;
        }
    }

    private void RegisterVipFeatures()
    {
        if (_vipApi == null || _isFeatureRegistered) return;

        _vipApi.RegisterFeature(FeatureKey, FeatureType.Toggle, null,
        displayNameResolver: p => Core.Translation.GetPlayerLocalizer(p)["vip.antiflash"]);
        _vipApi.DescribeFeature(FeatureKey, p => Core.Translation.GetPlayerLocalizer(p)[ReadMode(p) switch
        {
            AntiFlashMode.Teammates => "vip.antiflash.description.teammates",
            AntiFlashMode.Own => "vip.antiflash.description.own",
            AntiFlashMode.TeammatesAndOwn => "vip.antiflash.description.teammates_own",
            _ => "vip.antiflash.description",
        }]);

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

public enum AntiFlashMode
{
    All = 0,
    Teammates = 1,
    Own = 2,
    TeammatesAndOwn = 3
}

public class AntiFlashConfig
{
    public AntiFlashMode Mode { get; set; } = AntiFlashMode.All;
}