using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.SchemaDefinitions;
using VIPCore.Contract;
using Microsoft.Extensions.Logging;

namespace VIP_FastReload;

[PluginMetadata(Id = "VIP_FastReload", Version = "1.0.0", Name = "[VIP] FastReload", Author = "aga", Description = "No description.")]
public partial class VIP_FastReload : BasePlugin
{
    private const string FeatureKey = "vip.fastreload";

    private readonly Dictionary<uint, long> _lastAutoReloadMs = new();

    private IVipCoreApiV1? _vipApi;
    private bool _isFeatureRegistered;

    public VIP_FastReload(ISwiftlyCore core) : base(core)
    {
    }

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
    {
    }

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
        Core.GameEvent.HookPost<EventWeaponReload>(OnWeaponReload);
        Core.GameEvent.HookPost<EventWeaponFire>(OnWeaponFire);
        Core.GameEvent.HookPost<EventWeaponFireOnEmpty>(OnWeaponFireOnEmpty);
        RegisterVipFeaturesWhenReady();
    }

    private HookResult OnWeaponFireOnEmpty(EventWeaponFireOnEmpty @event)
    {
        if (_vipApi == null) return HookResult.Continue;

        var player = @event.UserIdPlayer;
        if (player == null || player.IsFakeClient || !player.IsValid) return HookResult.Continue;

        if (!_vipApi.IsClientVip(player)) return HookResult.Continue;
        if (_vipApi.GetPlayerFeatureState(player, FeatureKey) != FeatureState.Enabled) return HookResult.Continue;

        var controller = player.Controller;
        if (controller == null || !controller.IsValid) return HookResult.Continue;

        var now = Environment.TickCount64;
        if (_lastAutoReloadMs.TryGetValue(controller.Index, out var last) && now - last < 200)
            return HookResult.Continue;
        _lastAutoReloadMs[controller.Index] = now;

        ApplyFastReload(player);
        return HookResult.Continue;
    }

    private HookResult OnWeaponFire(EventWeaponFire @event)
    {
        if (_vipApi == null) return HookResult.Continue;

        var player = @event.UserIdPlayer;
        if (player == null || player.IsFakeClient || !player.IsValid) return HookResult.Continue;

        if (!_vipApi.IsClientVip(player)) return HookResult.Continue;
        if (_vipApi.GetPlayerFeatureState(player, FeatureKey) != FeatureState.Enabled) return HookResult.Continue;

        var controller = player.Controller;
        if (controller == null || !controller.IsValid) return HookResult.Continue;

        var pawn = controller.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid) return HookResult.Continue;

        var ws = pawn.WeaponServices;
        if (ws == null) return HookResult.Continue;

        var now = Environment.TickCount64;
        if (_lastAutoReloadMs.TryGetValue(controller.Index, out var last) && now - last < 200)
            return HookResult.Continue;

        Core.Scheduler.NextTick(() =>
        {
            if (controller == null || !controller.IsValid) return;
            var pawn2 = controller.PlayerPawn.Value;
            if (pawn2 == null || !pawn2.IsValid) return;
            var ws2 = pawn2.WeaponServices;
            if (ws2 == null) return;
            var activeWeapon2 = ws2.ActiveWeapon.Value;
            if (activeWeapon2 == null || !activeWeapon2.IsValid) return;

            if (activeWeapon2.Clip1 != 0) return;

            var now2 = Environment.TickCount64;
            if (_lastAutoReloadMs.TryGetValue(controller.Index, out var last2) && now2 - last2 < 200)
                return;
            _lastAutoReloadMs[controller.Index] = now2;
            ApplyFastReload(player);
        });

        return HookResult.Continue;
    }

    private HookResult OnWeaponReload(EventWeaponReload @event)
    {
        if (_vipApi == null) return HookResult.Continue;

        var player = @event.UserIdPlayer;
        if (player == null || player.IsFakeClient || !player.IsValid) return HookResult.Continue;

        if (!_vipApi.IsClientVip(player)) return HookResult.Continue;
        if (_vipApi.GetPlayerFeatureState(player, FeatureKey) != FeatureState.Enabled) return HookResult.Continue;

        ApplyFastReload(player);

        return HookResult.Continue;
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
            return;
        },
        displayNameResolver: p => Core.Translation.GetPlayerLocalizer(p)["vip.fastreload"]);

        _isFeatureRegistered = true;
    }

    private void ApplyFastReload(SwiftlyS2.Shared.Players.IPlayer player)
    {
        var controller = player.Controller;
        if (controller == null || !controller.IsValid) return;

        var pawn = controller.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid) return;

        var weaponServices = pawn.WeaponServices;
        if (weaponServices == null) return;

        void ApplyCore(bool allowAnimationReset)
        {
            var activeWeapon = weaponServices.ActiveWeapon.Value;
            if (activeWeapon == null || !activeWeapon.IsValid) return;

            var csWeapon = Core.EntitySystem.GetEntityByIndex<CCSWeaponBase>(activeWeapon.Index);
            if (csWeapon == null) return;

            var wasReloading = csWeapon.InReload;

            if (wasReloading)
            {
                csWeapon.InReload = false;
                csWeapon.InReloadUpdated();
            }

            var weaponVData = activeWeapon.PlayerWeaponVData;
            if (weaponVData == null) return;

            var maxClip = weaponVData.MaxClip1;
            if (maxClip <= 0) maxClip = weaponVData.DefaultClip1;
            if (maxClip <= 0) return;

            if (activeWeapon.Clip1 < maxClip)
            {
                activeWeapon.Clip1 = maxClip;
                activeWeapon.Clip1Updated();
            }

            if (allowAnimationReset)
            {
                var designerName = activeWeapon.DesignerName;
                Core.Scheduler.NextTick(() =>
                {
                    if (pawn == null || !pawn.IsValid) return;
                    var ws = pawn.WeaponServices;
                    if (ws == null) return;

                    var currentWeapon = ws.ActiveWeapon.Value;
                    if (currentWeapon == null || !currentWeapon.IsValid) return;

                    var currentCsWeapon = Core.EntitySystem.GetEntityByIndex<CCSWeaponBase>(currentWeapon.Index);
                    if (currentCsWeapon != null && currentCsWeapon.InReload)
                    {
                        currentCsWeapon.InReload = false;
                        currentCsWeapon.InReloadUpdated();
                    }

                    ws.SelectWeaponByDesignerName("weapon_knife");
                    Core.Scheduler.NextTick(() =>
                    {
                        if (pawn == null || !pawn.IsValid) return;
                        var ws2 = pawn.WeaponServices;
                        if (ws2 == null) return;
                        ws2.SelectWeaponByDesignerName(designerName);
                    });
                });
            }
        }

        ApplyCore(allowAnimationReset: true);
        Core.Scheduler.NextTick(() => ApplyCore(allowAnimationReset: false));
    }

    public override void Unload()
    {
        if (_vipApi != null)
        {
            _vipApi.OnCoreReady -= RegisterVipFeatures;
            if (_isFeatureRegistered)
                _vipApi.UnregisterFeature(FeatureKey);
        }
    }
}