using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using VIPCore.Contract;

namespace VIP_ChatColor;

/// <summary>
/// Per-group config for the vip.chatcolor feature.
/// Configure in groups.jsonc under Values > vip.chatcolor:
/// {
///   "Global": "[red]",     // applied when no team-specific color is set
///   "CT":     "[blue]",    // overrides Global for CT players
///   "T":      "[orange]"   // overrides Global for T players
/// }
///
/// Supported color tags:
///   [default] [white] [darkred] [lightpurple] [green] [olive] [lime]
///   [red] [gray] [grey] [yellow] [lightyellow] [silver] [bluegrey]
///   [lightblue] [blue] [darkblue] [purple] [magenta] [lightred]
///   [gold] [orange]
///
/// Leave CT / T empty (or omit) to fall back to Global.
/// </summary>
public class ChatColorConfig
{
    public string Global { get; set; } = "";
    public string CT     { get; set; } = "";
    public string T      { get; set; } = "";
}

[PluginMetadata(Id = "VIP_ChatColor", Version = "1.0.0", Name = "VIP_ChatColor", Author = "aga", Description = "Colored chat names for VIP players.")]
public partial class VIP_ChatColor : BasePlugin
{
    private const string FeatureKey = "vip.chatcolor";

    private IVipCoreApiV1? _vipApi;
    private bool _isFeatureRegistered;

    private Guid _chatHookGuid;
    private bool _chatHookRegistered;

    public VIP_ChatColor(ISwiftlyCore core) : base(core) { }

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager) { }

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        _vipApi = null;
        _isFeatureRegistered = false;

        if (interfaceManager.HasSharedInterface("VIPCore.Api.v1"))
            _vipApi = interfaceManager.GetSharedInterface<IVipCoreApiV1>("VIPCore.Api.v1");

        RegisterVipFeaturesWhenReady();
    }

    public override void Load(bool hotReload)
    {
        _chatHookGuid = Core.Command.HookClientChat(OnClientChat);
        _chatHookRegistered = true;

        RegisterVipFeaturesWhenReady();
    }

    public override void Unload()
    {
        if (_chatHookRegistered)
        {
            Core.Command.UnhookClientChat(_chatHookGuid);
            _chatHookRegistered = false;
        }

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

        _vipApi.RegisterFeature(FeatureKey, FeatureType.Toggle, null,
            displayNameResolver: p => Core.Translation.GetPlayerLocalizer(p)["vip.chatcolor"]);

        _isFeatureRegistered = true;
    }

    private HookResult OnClientChat(int playerId, string text, bool teamonly)
    {
        if (_vipApi == null) return HookResult.Continue;

        var player = Core.PlayerManager.GetPlayer(playerId);
        if (player == null || player.IsFakeClient || !player.IsValid)
            return HookResult.Continue;

        if (!_vipApi.IsClientVip(player)) return HookResult.Continue;
        if (!_vipApi.PlayerHasFeature(player, FeatureKey)) return HookResult.Continue;
        if (_vipApi.GetPlayerFeatureState(player, FeatureKey) != FeatureState.Enabled)
            return HookResult.Continue;

        var config = _vipApi.GetFeatureValue<ChatColorConfig>(player, FeatureKey);
        if (config == null) return HookResult.Continue;

        var controller = player.Controller;
        if (controller == null || !controller.IsValid) return HookResult.Continue;

        byte teamNum = controller.TeamNum;

        // Pick team-specific color tag, fall back to global
        var colorTag = config.Global ?? "";
        if (teamNum == (byte)Team.CT && !string.IsNullOrEmpty(config.CT))
            colorTag = config.CT;
        else if (teamNum == (byte)Team.T && !string.IsNullOrEmpty(config.T))
            colorTag = config.T;

        if (string.IsNullOrEmpty(colorTag)) return HookResult.Continue;

        var playerName = controller.PlayerName;
        var teamLabel  = teamonly ? "(Team) " : "";

        // Helper.Colored() resolves [blue], [red], [default] etc. into CS2 chat escape bytes
        var message = $"{colorTag}{teamLabel}{playerName}[default]: {text}".Colored();

        if (teamonly)
        {
            foreach (var p in Core.PlayerManager.GetAllValidPlayers())
            {
                if (!p.IsFakeClient && p.Controller != null && p.Controller.TeamNum == teamNum)
                    p.SendChat(message);
            }
        }
        else
        {
            Core.PlayerManager.SendChat(message);
        }

        return HookResult.Stop;
    }
}
