using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Translation;
using Cookies.Contract;
using VIPCore.Services;
using VIPCore.Database;
using VIPCore.Database.Repositories;
using VIPCore.Config;
using VIPCore.Contract;
using VIPCore.Api;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace VIPCore;

[PluginMetadata(Id = "VIPCore", Version = "1.0.0", Name = "VIPCore", Author = "aga", Description = "Core VIP management plugin ported to SwiftlyS2.")]
public partial class VIPCore : BasePlugin
{
    private VipConfig? _config;
    private GroupsConfig? _groupsConfig;

    private Cookies.Contract.IPlayerCookiesAPIv1? _playerCookiesApi;
    private IInterfaceManager? _interfaceManager;
    private CancellationTokenSource? _cookiesResolveRetryCts;

    private readonly VipCoreApiV1 _vipCoreApi;

    private IServiceProvider? _serviceProvider;

    private void TryResolveCookiesApi()
    {
        if (_interfaceManager == null)
            return;

        try
        {
            if (!_interfaceManager.HasSharedInterface("Cookies.Player.v1"))
                return;

            var cookiesApi = _interfaceManager.GetSharedInterface<Cookies.Contract.IPlayerCookiesAPIv1>("Cookies.Player.v1");
            if (cookiesApi == null)
                return;

            _playerCookiesApi = cookiesApi;
            _serviceProvider?.GetRequiredService<CookieService>().SetPlayerCookiesApi(_playerCookiesApi);

            _cookiesResolveRetryCts?.Cancel();
            _cookiesResolveRetryCts = null;
        }
        catch (Exception ex)
        {
            Core.Logger.LogWarning(ex, "[VIPCore] Failed to resolve Cookies.Player.v1 shared interface.");
        }
    }

    private void OnClientPutInServer(SwiftlyS2.Shared.Events.IOnClientPutInServerEvent @event)
    {
        var player = Core.PlayerManager.GetPlayer(@event.PlayerId);
        if (player == null || player.IsFakeClient) return;

        _vipCoreApi.RaiseOnPlayerSpawn(player);

        var vipService = _serviceProvider!.GetRequiredService<VipService>();
        var steamId = player.SteamID;

        Task.Run(async () =>
        {
            try
            {
                await vipService.LoadPlayer(player);

                var vipUser = vipService.GetVipUser(steamId);
                Core.Scheduler.NextTick(() =>
                {
                    if (!player.IsValid) return;
                    if (vipUser != null)
                    {
                        if (_config?.VipLogging == true)
                            Core.Logger.LogDebug("[VIPCore] OnClientPutInServer: Raising PlayerLoaded for {Name} with group {Group}", player.Controller?.PlayerName ?? "unknown", vipUser.group);
                        _vipCoreApi.RaisePlayerLoaded(player, vipUser.group);
                    }
                    else
                    {
                        if (_config?.VipLogging == true)
                            Core.Logger.LogDebug("[VIPCore] OnClientPutInServer: Player {Name} is not VIP, not raising PlayerLoaded", player.Controller?.PlayerName ?? "unknown");
                    }
                });
            }
            catch (Exception ex)
            {
                Core.Logger.LogError(ex, "[VIPCore] Failed to load player {SteamId} on connect.", steamId);
            }
        });
    }

    private void OnClientDisconnected(SwiftlyS2.Shared.Events.IOnClientDisconnectedEvent @event)
    {
        var player = Core.PlayerManager.GetPlayer(@event.PlayerId);
        if (player == null || player.IsFakeClient) return;

        var vipService = _serviceProvider!.GetRequiredService<VipService>();

        var vipUser = vipService.GetVipUser(player.SteamID);
        vipService.UnloadPlayer(player);

        if (vipUser != null)
        {
            _vipCoreApi.RaisePlayerRemoved(player, vipUser.group);
        }
    }

    private void OnSteamAPIActivated()
    {
        if (_serviceProvider == null) return;

        var serverIdentifier = _serviceProvider.GetRequiredService<ServerIdentifier>();
        Task.Run(async () =>
        {
            try
            {
                await serverIdentifier.InitializeAsync();
            }
            catch (Exception ex)
            {
                Core.Logger.LogError(ex, "[VIPCore] Failed to initialize server identifier on SteamAPI activation.");
            }
        });
    }

    public VIPCore(ISwiftlyCore core) : base(core)
    {
        _vipCoreApi = new VipCoreApiV1(core);
    }

    private IPlayer? FindOnlinePlayerBySteamId(ulong steamId)
    {
        for (var i = 0; i < Core.PlayerManager.PlayerCap; i++)
        {
            var p = Core.PlayerManager.GetPlayer(i);
            if (p == null || p.IsFakeClient) continue;
            if (p.SteamID == steamId) return p;
        }

        return null;
    }

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
    {
        try
        {
            if (_vipCoreApi == null)
            {
                Core.Logger.LogError("[VIPCore] Cannot register shared interface: API instance is null");
                return;
            }

            Core.Logger.LogInformation("[VIPCore] Registering shared interface 'VIPCore.Api.v1' as VIPCore.Contract.IVipCoreApiV1");

            interfaceManager.AddSharedInterface<IVipCoreApiV1, VipCoreApiV1>("VIPCore.Api.v1", _vipCoreApi);

            // Verify registration
            if (interfaceManager.HasSharedInterface("VIPCore.Api.v1"))
            {
                Core.Logger.LogInformation("[VIPCore] Successfully registered and verified shared interface 'VIPCore.Api.v1'");
            }
            else
            {
                Core.Logger.LogError("[VIPCore] Interface registration appeared to succeed but HasSharedInterface returns false!");
            }
        }
        catch (Exception ex)
        {
            Core.Logger.LogError(ex, "[VIPCore] Failed to register shared interface: {Message}", ex.Message);
            Core.Logger.LogError("[VIPCore] Stack trace: {StackTrace}", ex.StackTrace);
        }
    }

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        _interfaceManager = interfaceManager;
        TryResolveCookiesApi();
    }

    public override void Load(bool hotReload)
    {
        // Hot-reload cleanup: tear down previous state to avoid double-subscriptions and leaked services
        if (hotReload)
        {
            Core.Event.OnSteamAPIActivated -= OnSteamAPIActivated;
            Core.Event.OnClientPutInServer -= OnClientPutInServer;
            Core.Event.OnClientDisconnected -= OnClientDisconnected;

            if (_serviceProvider != null)
            {
                var cookieSvc = _serviceProvider.GetRequiredService<CookieService>();
                cookieSvc.SaveCookies();
                (_serviceProvider as IDisposable)?.Dispose();
                _serviceProvider = null;
            }

            _cookiesResolveRetryCts?.Cancel();
            _cookiesResolveRetryCts = null;
        }

        // Initialize Configuration using framework methods
        Core.Configuration.InitializeJsonWithModel<VipConfig>("config.jsonc", "vip");
        Core.Configuration.InitializeJsonWithModel<GroupsConfig>("vip_groups.jsonc", "vip_groups");

        var configPath = Core.Configuration.GetConfigPath("config.jsonc");
        var groupsPath = Core.Configuration.GetConfigPath("vip_groups.jsonc");

        // Register the files into the manager
        Core.Configuration.Configure(builder =>
        {
            builder.AddJsonFile(configPath, optional: false, reloadOnChange: true);
            builder.AddJsonFile(groupsPath, optional: false, reloadOnChange: true);
        });

        _config = Core.Configuration.Manager.GetSection("vip").Get<VipConfig>() ?? new VipConfig();

        // Exhaustive Search for Groups in the entire Configuration Manager
        var foundGroups = new Dictionary<string, VipGroup>();

        void SearchForGroups(IConfiguration section)
        {
            if (section is IConfigurationSection s && s.Key == "Groups")
            {
                foreach (var groupSection in section.GetChildren())
                {
                    if (groupSection is IConfigurationSection groupSectionCast)
                    {
                        var groupName = groupSectionCast.Key;
                        var valuesSection = groupSectionCast.GetSection("Values");
                        var values = valuesSection.Get<Dictionary<string, object>>();

                        if (values != null && !foundGroups.ContainsKey(groupName))
                        {
                            var weight = 0;
                            var weightStr = groupSectionCast.GetSection("Weight").Value;
                            if (!string.IsNullOrEmpty(weightStr))
                                int.TryParse(weightStr, out weight);

                            foundGroups[groupName] = new VipGroup { Weight = weight, Values = values, ValuesSection = valuesSection };
                        }
                    }
                }
            }

            foreach (var child in section.GetChildren())
            {
                SearchForGroups(child);
            }
        }

        SearchForGroups(Core.Configuration.Manager);

        _groupsConfig = new GroupsConfig { Groups = foundGroups };

        if (_groupsConfig.Groups.Count == 0)
        {
            // Fallback: Check if the section "vip_groups" exists and has children
            var vipGroupsSection = Core.Configuration.Manager.GetSection("vip_groups");
            if (vipGroupsSection.Exists())
            {
                var directGroups = vipGroupsSection.GetSection("Groups").Get<Dictionary<string, VipGroup>>();
                if (directGroups != null)
                {
                    foreach (var kvp in directGroups) foundGroups[kvp.Key] = kvp.Value;
                    _groupsConfig.Groups = foundGroups;
                }
            }
        }

        if (_config == null || _groupsConfig == null) return;

        // Dependency Injection
        var services = new ServiceCollection();
        services
            .AddSwiftly(Core)
            .AddSingleton(_groupsConfig!)
            .AddSingleton<DatabaseConnectionFactory>()
            .AddSingleton<IUserRepository, UserRepository>()
            .AddSingleton<CookieService>()
            .AddSingleton<FeatureService>()
            .AddSingleton<VipService>()
            .AddSingleton<MenuService>()
            .AddSingleton<ManageMenuService>()
            .AddSingleton<ServerIdentifier>();

        // Register VipConfig via IOptionsMonitor<T> for hot-reload support
        services.AddOptionsWithValidateOnStart<VipConfig>().BindConfiguration("vip");

        _serviceProvider = services.BuildServiceProvider();

        _vipCoreApi.SetServiceProvider(_serviceProvider);

        try
        {
            var connectionFactory = _serviceProvider.GetRequiredService<DatabaseConnectionFactory>();
            using var dbConnection = connectionFactory.CreateConnection();
            MigrationRunner.RunMigrations(dbConnection);
        }
        catch (Exception ex)
        {
            Core.Logger.LogError(ex, "[VIPCore] Failed to run database migrations.");
        }

        // Server auto-detection: on hot-reload Steam API is already active, so initialize immediately
        Core.Event.OnSteamAPIActivated += OnSteamAPIActivated;
        if (hotReload)
        {
            var serverIdentifier = _serviceProvider.GetRequiredService<ServerIdentifier>();
            Task.Run(async () =>
            {
                try
                {
                    await serverIdentifier.InitializeAsync();
                }
                catch (Exception ex)
                {
                    Core.Logger.LogError(ex, "[VIPCore] Failed to initialize server identifier on hot-reload.");
                }
            });
        }

        // Initialize Services
        var cookieService = _serviceProvider.GetRequiredService<CookieService>();
        cookieService.SetPlayerCookiesApi(_playerCookiesApi);
        cookieService.LoadCookies();

        if (_playerCookiesApi == null)
        {
            _cookiesResolveRetryCts?.Cancel();
            _cookiesResolveRetryCts = Core.Scheduler.RepeatBySeconds(2f, () => TryResolveCookiesApi());
        }

        Core.Event.OnClientPutInServer += OnClientPutInServer;
        Core.Event.OnClientDisconnected += OnClientDisconnected;

        _vipCoreApi.RaiseOnCoreReady();

        Core.Logger.LogInformation("[VIPCore] Plugin loaded successfully.");
    }

    public override void Unload()
    {
        Core.Event.OnSteamAPIActivated -= OnSteamAPIActivated;
        Core.Event.OnClientPutInServer -= OnClientPutInServer;
        Core.Event.OnClientDisconnected -= OnClientDisconnected;

        var serviceProvider = _serviceProvider;
        if (serviceProvider == null)
        {
            return;
        }

        var cookieService = serviceProvider.GetRequiredService<CookieService>();
        cookieService.SaveCookies();

        (serviceProvider as IDisposable)?.Dispose();
        _serviceProvider = null;

        Core.Logger.LogInformation("[VIPCore] Plugin unloaded.");
    }

    [Command("vip")]
    public void OnVipCommand(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            context.Reply("This command can only be used by players!");
            return;
        }

        var serviceProvider = _serviceProvider;
        if (serviceProvider == null)
        {
            context.Reply("VIPCore is not initialized.");
            return;
        }

        var player = context.Sender!;
        if (player == null) return;

        var vipService = serviceProvider.GetRequiredService<VipService>();

        Task.Run(async () =>
        {
            try
            {
                await vipService.LoadPlayer(player);
                var vipUser = vipService.GetVipUser(player.SteamID);

                Core.Scheduler.NextTick(() =>
                {
                    var localizer = Core.Translation.GetPlayerLocalizer(player);

                    if (vipUser == null)
                    {
                        player.SendMessage(MessageType.Chat, localizer["vip.NoAccess"]);
                        return;
                    }

                    var menuService = serviceProvider.GetRequiredService<MenuService>();
                    menuService.OpenVipMenu(player, vipUser);
                });
            }
            catch (Exception ex)
            {
                Core.Logger.LogError(ex, "[VIPCore] Failed to load VIP data for player.");
            }
        });
    }

    [Command("vip_adduser", permission: "vipcore.adduser")]
    public void OnAddUserCommand(ICommandContext context)
    {
        if (_serviceProvider == null)
        {
            Core.Scheduler.NextTick(() =>
            {
                context.Reply("VIPCore is not initialized.");
            });
            return;
        }

        if (context.Args.Length < 3)
        {
            Core.Scheduler.NextTick(() =>
            {
                context.Reply("Usage: vip_adduser <steamid> <group> <time>");
            });
            return;
        }

        if (!ulong.TryParse(context.Args[0], out var steamId)) return;
        var group = context.Args[1];
        if (!int.TryParse(context.Args[2], out var time)) return;

        var vipService = _serviceProvider.GetRequiredService<VipService>();

        Task.Run(async () =>
        {
            try
            {
                await vipService.AddVip(steamId, "unknown", group, time);

                Core.Scheduler.NextTick(() =>
                {
                    var target = FindOnlinePlayerBySteamId(steamId);
                    if (target != null)
                    {
                        Task.Run(async () =>
                        {
                            try
                            {
                                await vipService.LoadPlayer(target);
                            }
                            catch (Exception loadEx)
                            {
                                Core.Logger.LogError(loadEx, "[VIPCore] Failed to load newly added VIP player {SteamId}", steamId);
                            }
                        });
                    }
                });

                Core.Scheduler.NextTick(() =>
                {
                    context.Reply($"Added VIP: {steamId} to group {group}");
                });
            }
            catch (Exception ex)
            {
                Core.Logger.LogError(ex, "[VIPCore] Failed to add VIP user {SteamId}", steamId);
                Core.Scheduler.NextTick(() =>
                {
                    context.Reply($"Failed to add VIP: {ex.Message}");
                });
            }
        });
    }

    [Command("vip_manage", permission: "vipcore.manage")]
    public void OnManageCommand(ICommandContext context)
    {
        if (!context.IsSentByPlayer)
        {
            context.Reply("This command can only be used by players!");
            return;
        }

        var serviceProvider = _serviceProvider;
        if (serviceProvider == null)
        {
            context.Reply("VIPCore is not initialized.");
            return;
        }

        var player = context.Sender!;
        if (player == null) return;

        var manageMenuService = serviceProvider.GetRequiredService<ManageMenuService>();
        manageMenuService.OpenManageMenu(player);
    }

    [Command("vip_deleteuser", permission: "vipcore.deleteuser")]
    public void OnDeleteUserCommand(ICommandContext context)
    {
        if (_serviceProvider == null)
        {
            Core.Scheduler.NextTick(() =>
            {
                context.Reply("VIPCore is not initialized.");
            });
            return;
        }

        if (context.Args.Length < 1)
        {
            Core.Scheduler.NextTick(() =>
            {
                context.Reply("Usage: vip_deleteuser <steamid>");
            });
            return;
        }

        if (!ulong.TryParse(context.Args[0], out var steamId)) return;

        var vipService = _serviceProvider.GetRequiredService<VipService>();

        Task.Run(async () =>
        {
            try
            {
                await vipService.RemoveVip(steamId);
                Core.Scheduler.NextTick(() =>
                {
                    context.Reply($"Removed VIP: {steamId}");
                });
            }
            catch (Exception ex)
            {
                Core.Logger.LogError(ex, "[VIPCore] Failed to remove VIP user {SteamId}", steamId);
                Core.Scheduler.NextTick(() =>
                {
                    context.Reply($"Failed to remove VIP: {ex.Message}");
                });
            }
        });
    }
}