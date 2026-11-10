using System;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.SchemaDefinitions;
using VIPCore.Contract;

namespace VIP_Fov;

[PluginMetadata(Id = "VIP_Fov", Version = "1.1.1", Name = "VIP_Fov", Author = "aga", Description = "Allows VIPs to change their FOV.")]
public partial class VIP_Fov : BasePlugin {
    private const uint DefaultFov = 90;
    private const string FeatureKey = "vip.fov";
    private static readonly string FeatureValueCookieKey = FeatureKey + ".value";

    private static readonly uint[] AllowedFovs = { 90, 100, 110, 120 };

    private IVipCoreApiV1? _vipApi;
    private bool _isFeatureRegistered;
    private readonly uint[] _fovSettings = new uint[65];

    public VIP_Fov(ISwiftlyCore core) : base(core)
    {
    }

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager) {
    }

    public override void UseSharedInterface(IInterfaceManager interfaceManager) {
        _vipApi = null;

        if (interfaceManager.HasSharedInterface("VIPCore.Api.v1"))
            _vipApi = interfaceManager.GetSharedInterface<IVipCoreApiV1>("VIPCore.Api.v1");

        RegisterVipFeaturesWhenReady();
    }

    public override void Load(bool hotReload) {
        for (var i = 0; i < _fovSettings.Length; i++)
            _fovSettings[i] = DefaultFov;

        Core.Event.OnClientConnected += OnClientConnected;
        Core.Event.OnClientDisconnected += OnClientDisconnected;

        RegisterVipFeaturesWhenReady();
    }

    public override void Unload() {
        Core.Event.OnClientConnected -= OnClientConnected;
        Core.Event.OnClientDisconnected -= OnClientDisconnected;

        if (_vipApi != null)
        {
            if (_isFeatureRegistered)
                _vipApi.UnregisterFeature(FeatureKey);
                
            _vipApi.OnPlayerSpawn -= OnPlayerSpawn;
            _vipApi.PlayerLoaded -= OnPlayerLoaded;
            _vipApi.PlayerRemoved -= OnPlayerRemoved;
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

        _vipApi.RegisterFeature(FeatureKey, FeatureType.Selectable, (player, _) =>
        {
            Core.Scheduler.NextTick(() =>
            {
                if (player.IsValid) _vipApi?.OpenMenu(player, VipMenuIds.Customize);
            });
        },
        displayNameResolver: p => Core.Translation.GetPlayerLocalizer(p)["vip.fov"]);
        _vipApi.DescribeFeature(FeatureKey, p => Core.Translation.GetPlayerLocalizer(p)["vip.fov.description"]);
        _vipApi.RegisterFeatureOptions(FeatureKey, GetFovOptions, GetCurrentFov, ChooseFov);
        
        _isFeatureRegistered = true;
        _vipApi.OnPlayerSpawn += OnPlayerSpawn;
        _vipApi.PlayerLoaded += OnPlayerLoaded;
        _vipApi.PlayerRemoved += OnPlayerRemoved;
    }

    private void OnPlayerLoaded(IPlayer player, string group)
    {
        if (_vipApi == null) return;
        if (!player.IsValid || player.IsFakeClient) return;
        if (!_vipApi.PlayerHasFeature(player, FeatureKey))
        {
            ResetFov(player);
            return;
        }

        LoadFovFromCookie(player);
        ApplyFov(player);
    }

    private void OnPlayerRemoved(IPlayer player, string group)
    {
        if (!player.IsValid || player.IsFakeClient) return;
        ResetFov(player);
    }

    // Back to the game's FOV for a player who no longer has the feature; their saved choice is kept.
    private void ResetFov(IPlayer player)
    {
        if (player.Slot < 0 || player.Slot >= _fovSettings.Length || _fovSettings[player.Slot] == DefaultFov) return;
        SetFov(player, DefaultFov, save: false);
    }

    // The game's own FOV first, then each wider one described by where it sits.
    private IReadOnlyList<VipFeatureOption> GetFovOptions(IPlayer player)
    {
        var localizer = Core.Translation.GetPlayerLocalizer(player);
        var wider = AllowedFovs.Where(f => f > DefaultFov).OrderBy(f => f).ToArray();

        var options = new List<VipFeatureOption>(wider.Length + 1)
        {
            new(FovId(DefaultFov), FovId(DefaultFov), Description: localizer["fov.Default"], IsDefault: true),
        };
        for (var i = 0; i < wider.Length; i++)
        {
            var description = wider.Length == 1 ? "fov.Wider"
                : i == 0 ? "fov.LittleWider"
                : i == wider.Length - 1 ? "fov.Widest"
                : "fov.Wider";
            options.Add(new(FovId(wider[i]), FovId(wider[i]), Description: localizer[description]));
        }

        return options;
    }

    private string? GetCurrentFov(IPlayer player)
    {
        if (player.Slot < 0 || player.Slot >= _fovSettings.Length) return null;
        return FovId(_fovSettings[player.Slot]);
    }

    private void ChooseFov(IPlayer player, string id)
    {
        if (_vipApi == null || !player.IsValid || !_vipApi.PlayerHasFeature(player, FeatureKey)) return;
        if (!uint.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var fov) || !AllowedFovs.Contains(fov)) return;

        SetFov(player, fov, save: true);

        var localizer = Core.Translation.GetPlayerLocalizer(player);
        player.SendMessage(MessageType.Chat, fov == DefaultFov ? localizer["fov.Off"] : localizer["fov.On", FovId(fov)]);
    }

    private static string FovId(uint fov) => fov.ToString(CultureInfo.InvariantCulture);

    private void OnClientConnected(SwiftlyS2.Shared.Events.IOnClientConnectedEvent @event)
    {
        if (@event.PlayerId < 0 || @event.PlayerId >= _fovSettings.Length) return;
        _fovSettings[@event.PlayerId] = DefaultFov;
    }

    private void OnClientDisconnected(SwiftlyS2.Shared.Events.IOnClientDisconnectedEvent @event)
    {
        if (@event.PlayerId < 0 || @event.PlayerId >= _fovSettings.Length) return;
        _fovSettings[@event.PlayerId] = DefaultFov;
    }

    private void OnPlayerSpawn(IPlayer player)
    {
        if (_vipApi == null) return;
        if (!player.IsValid || player.IsFakeClient) return;
        if (!_vipApi.PlayerHasFeature(player, FeatureKey))
        {
            ResetFov(player);
            return;
        }

        LoadFovFromCookie(player);
        ApplyFov(player);
    }

    private void LoadFovFromCookie(IPlayer player)
    {
        if (_vipApi == null) return;
        if (!player.IsValid) return;
        if (player.Slot < 0 || player.Slot >= _fovSettings.Length) return;

        int raw = 0;
        try
        {
            raw = _vipApi.GetPlayerCookie<int>(player, FeatureValueCookieKey);
        }
        catch
        {
            raw = 0;
        }

        if (raw <= 0)
        {
            _fovSettings[player.Slot] = DefaultFov;
            return;
        }

        var value = (uint)raw;
        var finalFov = AllowedFovs.Contains(value) ? value : DefaultFov;
        _fovSettings[player.Slot] = finalFov;
    }

    private void SetFov(IPlayer player, uint fov, bool save)
    {
        if (!player.IsValid) return;
        if (player.Slot < 0 || player.Slot >= _fovSettings.Length) return;

        _fovSettings[player.Slot] = fov;
        ApplyFov(player);

        if (!save) return;

        if (_vipApi == null) return;
        var storeValue = fov == DefaultFov ? 0 : (int)fov;
        _vipApi.SetPlayerCookie(player, FeatureValueCookieKey, storeValue);
    }

    private void ApplyFov(IPlayer player)
    {
        var controller = player.Controller as CCSPlayerController;
        if (controller == null || !controller.IsValid)
            return;

        uint fov = _fovSettings[player.Slot];
        controller.DesiredFOV = fov;
        controller.DesiredFOVUpdated();
    }
}
