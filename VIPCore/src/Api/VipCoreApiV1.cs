using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using VIPCore.Contract;
using VIPCore.Services;
using VIPCore.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Linq;

namespace VIPCore.Api;

public class VipCoreApiV1 : IVipCoreApiV1
{
    private readonly ISwiftlyCore _core;
    private IServiceProvider? _serviceProvider;

    public VipCoreApiV1(ISwiftlyCore core)
    {
        _core = core;
    }

    internal void SetServiceProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    private FeatureService FeatureService => _serviceProvider!.GetRequiredService<FeatureService>();
    private VipService VipService => _serviceProvider!.GetRequiredService<VipService>();
    private GroupsConfig GroupsConfig => _serviceProvider!.GetRequiredService<GroupsConfig>();
    private CookieService CookieService => _serviceProvider!.GetRequiredService<CookieService>();
    private VipConfig Config => _serviceProvider!.GetRequiredService<IOptionsMonitor<VipConfig>>().CurrentValue;

    public event Action<IPlayer>? OnPlayerSpawn;
    public event Action<IPlayer, string>? PlayerLoaded;
    public event Action<IPlayer, string>? PlayerRemoved;
    public event Action? OnCoreReady;
    public event Func<IPlayer, string, FeatureState, FeatureType, bool?>? OnPlayerUseFeature;

    internal void RaiseOnCoreReady()
    {
        var handlerCount = OnCoreReady?.GetInvocationList().Length ?? 0;
        if (Config.VipLogging)
            _core.Logger.LogDebug("[VIPCore] Firing OnCoreReady event to {Count} subscribers", handlerCount);
        OnCoreReady?.Invoke();
    }

    internal void RaisePlayerLoaded(IPlayer player, string group) => PlayerLoaded?.Invoke(player, group);

    internal void RaisePlayerRemoved(IPlayer player, string group) => PlayerRemoved?.Invoke(player, group);

    internal void RaiseOnPlayerSpawn(IPlayer player) => OnPlayerSpawn?.Invoke(player);

    public void RegisterFeature(string featureKey, FeatureType featureType = FeatureType.Toggle, Action<IPlayer, FeatureState>? onSelectItem = null, Func<IPlayer, string>? displayNameResolver = null)
    {
        if (Config.VipLogging)
            _core.Logger.LogDebug("[VIPCore] Api: Registering feature '{Key}'", featureKey);
        
        if (_serviceProvider == null)
        {
            _core.Logger.LogWarning("[VIPCore] Api: Cannot register feature '{Key}' - ServiceProvider is null!", featureKey);
            return;
        }

        Action<IPlayer, FeatureState>? wrappedOnSelect = null;
        if (onSelectItem != null)
        {
            wrappedOnSelect = (player, state) =>
            {
                var handlers = OnPlayerUseFeature;
                if (handlers != null)
                {
                    foreach (Func<IPlayer, string, FeatureState, FeatureType, bool?> handler in handlers.GetInvocationList())
                    {
                        bool? allow = null;
                        try
                        {
                            allow = handler(player, featureKey, state, featureType);
                        }
                        catch
                        {
                        }

                        if (allow == false)
                        {
                            return;
                        }
                    }
                }

                onSelectItem(player, state);
            };
        }

        FeatureService.RegisterFeature(
            featureKey,
            featureType,
            wrappedOnSelect,
            displayNameResolver
        );

        VipService.InitializeFeatureForLoadedPlayers(featureKey);
    }

    public void UnregisterFeature(string featureKey)
    {
        FeatureService.UnregisterFeature(featureKey);
    }

    public T GetPlayerCookie<T>(IPlayer player, string key)
    {
        if (_serviceProvider == null) return default!;

        CookieService.LoadForPlayer(player);
        return CookieService.GetCookie<T>(player.SteamID, key);
    }

    public void SetPlayerCookie<T>(IPlayer player, string key, T value)
    {
        if (_serviceProvider == null) return;

        CookieService.SetCookie(player.SteamID, key, value);
    }

    public IEnumerable<string> GetAllRegisteredFeatures()
    {
        return FeatureService.GetRegisteredFeatures().Select(f => f.Key);
    }

    public FeatureState GetPlayerFeatureState(IPlayer player, string featureKey)
    {
        var user = VipService.GetVipUser(player.SteamID);
        if (user == null) return FeatureState.NoAccess;

        return user.FeatureStates.TryGetValue(featureKey, out var state)
            ? state
            : FeatureState.NoAccess;
    }

    public void SetPlayerFeatureState(IPlayer player, string featureKey, FeatureState newState)
    {
        var user = VipService.GetVipUser(player.SteamID);
        if (user == null) return;

        user.FeatureStates[featureKey] = newState;
    }

    public void DisableAllFeatures() => FeatureService.DisableAllFeatures();

    public void EnableAllFeatures() => FeatureService.EnableAllFeatures();

    public bool IsClientVip(IPlayer player) => VipService.IsClientVip(player);

    public void GiveClientVip(IPlayer player, string group, int time)
    {
        if (_serviceProvider == null) return;
        if (!player.IsValid) return;

        var accountId = player.SteamID;
        var name = player.Controller?.PlayerName ?? "unknown";

        Task.Run(async () =>
        {
            try
            {
                await VipService.AddVip(accountId, name, group, time);

                // Validate player is still valid before loading
                if (!player.IsValid) return;
                await VipService.LoadPlayer(player);

                // Validate player again before accessing SteamID
                if (!player.IsValid) return;
                var vipUser = VipService.GetVipUser(player.SteamID);
                if (vipUser != null)
                {
                    _core.Scheduler.NextTick(() =>
                    {
                        // Final validation in main thread
                        if (player.IsValid)
                        {
                            RaisePlayerLoaded(player, vipUser.group);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _core.Logger.LogWarning(ex, "[VIPCore] Failed to give temporary VIP to player {SteamId}", accountId);
            }
        });
    }

    public void RemoveClientVip(IPlayer player)
    {
        if (_serviceProvider == null) return;
        if (!IsClientVip(player)) return;

        var accountId = player.SteamID;
        var group = GetClientVipGroup(player);

        Task.Run(async () =>
        {
            await VipService.RemoveVip(accountId);

            _core.Scheduler.NextTick(() =>
            {
                if (!string.IsNullOrEmpty(group))
                {
                    RaisePlayerRemoved(player, group);
                }
            });
        });
    }

    public bool IsCoreReady() => _serviceProvider != null;

    public bool PlayerHasFeature(IPlayer player, string featureKey)
    {
        var state = GetPlayerFeatureState(player, featureKey);
        return state != FeatureState.NoAccess;
    }

    public string GetClientVipGroup(IPlayer player)
    {
        return VipService.GetVipUser(player.SteamID)?.group ?? string.Empty;
    }

    public string[] GetClientVipGroups(IPlayer player)
    {
        var user = VipService.GetVipUser(player.SteamID);
        return user?.OwnedGroups.ToArray() ?? Array.Empty<string>();
    }

    public string[] GetVipGroups()
    {
        return GroupsConfig.Groups.Keys.ToArray();
    }

    public T? GetFeatureValue<T>(IPlayer player, string featureKey) where T : class, new()
    {
        if (_serviceProvider == null) return default;

        var user = VipService.GetVipUser(player.SteamID);
        if (user == null) return default;

        var groupName = GroupsConfig.Groups.Keys
            .FirstOrDefault(k => k.Equals(user.group, StringComparison.OrdinalIgnoreCase));
        if (groupName == null || !GroupsConfig.Groups.TryGetValue(groupName, out var group))
            return default;

        if (group.ValuesSection == null) return default;

        var featureSection = group.ValuesSection.GetSection(featureKey);
        if (!featureSection.Exists()) return default;

        return featureSection.Get<T>() ?? new T();
    }
}
