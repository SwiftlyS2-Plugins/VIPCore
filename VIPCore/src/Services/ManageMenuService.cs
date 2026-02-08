using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Translation;
using SwiftlyS2.Core.Menus.OptionsBase;
using VIPCore.Config;
using VIPCore.Database.Repositories;
using VIPCore.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VIPCore.Services;

public class ManageMenuService(
    ISwiftlyCore core,
    VipService vipService,
    IUserRepository userRepository,
    IOptionsMonitor<VipConfig> coreConfigMonitor,
    GroupsConfig groupsConfig)
{
    private VipConfig coreConfig => coreConfigMonitor.CurrentValue;

    private string GetTimeModeKey(int timeMode) => timeMode switch
    {
        1 => "manage.TimeMode.Minutes",
        2 => "manage.TimeMode.Hours",
        3 => "manage.TimeMode.Days",
        _ => "manage.TimeMode.Seconds"
    };

    private (int value, string key)[] GetTimeOptions(int timeMode) => timeMode switch
    {
        1 => new[] { (30, "manage.Time.30Minutes"), (60, "manage.Time.1Hour"), (120, "manage.Time.2Hours"), (1440, "manage.Time.1Day"), (10080, "manage.Time.1Week"), (43200, "manage.Time.1Month") },
        2 => new[] { (1, "manage.Time.1Hour"), (2, "manage.Time.2Hours"), (6, "manage.Time.6Hours"), (12, "manage.Time.12Hours"), (24, "manage.Time.1Day"), (168, "manage.Time.1Week"), (720, "manage.Time.1Month") },
        3 => new[] { (1, "manage.Time.1Day"), (7, "manage.Time.1Week"), (14, "manage.Time.2Weeks"), (30, "manage.Time.1Month"), (90, "manage.Time.3Months"), (365, "manage.Time.1Year") },
        _ => new[] { (3600, "manage.Time.1Hour"), (86400, "manage.Time.1Day"), (604800, "manage.Time.1Week"), (2592000, "manage.Time.1Month"), (7776000, "manage.Time.3Months"), (31536000, "manage.Time.1Year") }
    };

    public void OpenManageMenu(IPlayer admin)
    {
        var localizer = core.Translation.GetPlayerLocalizer(admin);
        var builder = core.MenusAPI.CreateBuilder();
        builder.Design.SetMenuTitle(localizer["manage.Title"]);

        var addOption = new ButtonMenuOption(localizer["manage.AddVipUser"]);
        addOption.Click += async (sender, args) =>
        {
            core.Scheduler.NextTick(() => OpenAddVipSelectPlayerMenu(args.Player));
            await ValueTask.CompletedTask;
        };
        builder.AddOption(addOption);

        var manageOption = new ButtonMenuOption(localizer["manage.ManageVipUsers"]);
        manageOption.Click += async (sender, args) =>
        {
            core.Scheduler.NextTick(() =>
            {
                Task.Run(async () =>
                {
                    try
                    {
                        var users = await userRepository.GetAllUsersAsync(coreConfig.ServerId);
                        var userList = users.ToList();
                        core.Scheduler.NextTick(() => OpenManageUsersListMenu(args.Player, userList));
                    }
                    catch (Exception ex)
                    {
                        core.Logger.LogError(ex, "[VIPCore] Failed to load VIP users for manage menu.");
                        var loc = core.Translation.GetPlayerLocalizer(args.Player);
                        core.Scheduler.NextTick(() => args.Player.SendMessage(MessageType.Chat, loc["manage.chat.FailedLoadUsers"]));
                    }
                });
            });
            await ValueTask.CompletedTask;
        };
        builder.AddOption(manageOption);

        var menu = builder.Build();
        core.MenusAPI.OpenMenuForPlayer(admin, menu);
    }

    private void OpenAddVipSelectPlayerMenu(IPlayer admin)
    {
        var localizer = core.Translation.GetPlayerLocalizer(admin);
        var builder = core.MenusAPI.CreateBuilder();
        builder.Design.SetMenuTitle(localizer["manage.SelectPlayer"]);

        for (var i = 0; i < core.PlayerManager.PlayerCap; i++)
        {
            var player = core.PlayerManager.GetPlayer(i);
            if (player == null || player.IsFakeClient) continue;

            var steamId = player.SteamID;
            var playerName = player.Controller.PlayerName;

            var option = new ButtonMenuOption($"{playerName} ({steamId})");
            option.Click += async (sender, args) =>
            {
                core.Scheduler.NextTick(() => OpenAddVipSelectGroupMenu(args.Player, (long)steamId, playerName));
                await ValueTask.CompletedTask;
            };
            builder.AddOption(option);
        }

        var menu = builder.Build();
        core.MenusAPI.OpenMenuForPlayer(admin, menu);
    }

    private void OpenAddVipSelectGroupMenu(IPlayer admin, long steamId, string playerName)
    {
        var localizer = core.Translation.GetPlayerLocalizer(admin);
        var builder = core.MenusAPI.CreateBuilder();
        builder.Design.SetMenuTitle(localizer["manage.SelectGroup", playerName]);

        foreach (var groupName in groupsConfig.Groups.Keys)
        {
            var group = groupName;
            var option = new ButtonMenuOption(group);
            option.Click += async (sender, args) =>
            {
                core.Scheduler.NextTick(() => OpenAddVipSelectTimeMenu(args.Player, steamId, playerName, group));
                await ValueTask.CompletedTask;
            };
            builder.AddOption(option);
        }

        var menu = builder.Build();
        core.MenusAPI.OpenMenuForPlayer(admin, menu);
    }

    private void OpenAddVipSelectTimeMenu(IPlayer admin, long steamId, string playerName, string group)
    {
        var localizer = core.Translation.GetPlayerLocalizer(admin);
        var builder = core.MenusAPI.CreateBuilder();

        var timeModeLabel = localizer[GetTimeModeKey(coreConfig.TimeMode)];
        builder.Design.SetMenuTitle(localizer["manage.SelectDuration", timeModeLabel, playerName]);

        var timeOptions = GetTimeOptions(coreConfig.TimeMode);

        foreach (var (time, key) in timeOptions)
        {
            var t = time;
            var timeKey = key;
            var label = localizer[timeKey];
            var option = new ButtonMenuOption(label);
            option.Click += async (sender, args) =>
            {
                core.Scheduler.NextTick(() =>
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            await vipService.AddVip(steamId, playerName, group, t);

                            core.Scheduler.NextTick(() =>
                            {
                                var loc = core.Translation.GetPlayerLocalizer(args.Player);
                                var displayLabel = loc[timeKey];
                                args.Player.SendMessage(MessageType.Chat, loc["manage.chat.AddedVip", playerName, steamId, group, displayLabel]);

                                var target = core.PlayerManager.GetPlayerFromSteamId((ulong)steamId);
                                if (target != null)
                                {
                                    Task.Run(async () =>
                                    {
                                        try { await vipService.LoadPlayer(target); }
                                        catch (Exception ex) { core.Logger.LogError(ex, "[VIPCore] Failed to load newly added VIP player {SteamId}", steamId); }
                                    });
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            core.Logger.LogError(ex, "[VIPCore] Failed to add VIP user {SteamId}", steamId);
                            var loc = core.Translation.GetPlayerLocalizer(args.Player);
                            core.Scheduler.NextTick(() => args.Player.SendMessage(MessageType.Chat, loc["manage.chat.FailedAddVip", ex.Message]));
                        }
                    });
                });
                await ValueTask.CompletedTask;
            };
            builder.AddOption(option);
        }

        var permanentOption = new ButtonMenuOption(localizer["manage.Permanent"]);
        permanentOption.Click += async (sender, args) =>
        {
            core.Scheduler.NextTick(() =>
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await vipService.AddVip(steamId, playerName, group, 0);

                        core.Scheduler.NextTick(() =>
                        {
                            var loc = core.Translation.GetPlayerLocalizer(args.Player);
                            args.Player.SendMessage(MessageType.Chat, loc["manage.chat.AddedVipPermanent", playerName, steamId, group]);

                            var target = core.PlayerManager.GetPlayerFromSteamId((ulong)steamId);
                            if (target != null)
                            {
                                Task.Run(async () =>
                                {
                                    try { await vipService.LoadPlayer(target); }
                                    catch (Exception ex) { core.Logger.LogError(ex, "[VIPCore] Failed to load newly added VIP player {SteamId}", steamId); }
                                });
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        core.Logger.LogError(ex, "[VIPCore] Failed to add VIP user {SteamId}", steamId);
                        var loc = core.Translation.GetPlayerLocalizer(args.Player);
                        core.Scheduler.NextTick(() => args.Player.SendMessage(MessageType.Chat, loc["manage.chat.FailedAddVip", ex.Message]));
                    }
                });
            });
            await ValueTask.CompletedTask;
        };
        builder.AddOption(permanentOption);

        var menu = builder.Build();
        core.MenusAPI.OpenMenuForPlayer(admin, menu);
    }

    private void OpenManageUsersListMenu(IPlayer admin, List<User> users)
    {
        var localizer = core.Translation.GetPlayerLocalizer(admin);
        var builder = core.MenusAPI.CreateBuilder();
        builder.Design.SetMenuTitle(localizer["manage.ManageUsersList"]);

        if (users.Count == 0)
        {
            builder.AddOption(new TextMenuOption(localizer["manage.NoVipUsers"]));
        }
        else
        {
            foreach (var user in users)
            {
                var u = user;
                var expiresText = u.expires == 0 ? localizer["manage.Permanent"] : DateTimeOffset.FromUnixTimeSeconds(u.expires).ToString("yyyy-MM-dd HH:mm");
                var option = new ButtonMenuOption(localizer["manage.UserEntry", u.name, u.account_id, u.group]);
                option.Comment = localizer["manage.Expires", expiresText];
                option.Click += async (sender, args) =>
                {
                    core.Scheduler.NextTick(() => OpenUserManageMenu(args.Player, u));
                    await ValueTask.CompletedTask;
                };
                builder.AddOption(option);
            }
        }

        var menu = builder.Build();
        core.MenusAPI.OpenMenuForPlayer(admin, menu);
    }

    private void OpenUserManageMenu(IPlayer admin, User user)
    {
        var localizer = core.Translation.GetPlayerLocalizer(admin);
        var builder = core.MenusAPI.CreateBuilder();

        var expiresText = user.expires == 0 ? localizer["manage.Permanent"] : DateTimeOffset.FromUnixTimeSeconds(user.expires).ToString("yyyy-MM-dd HH:mm");
        builder.Design.SetMenuTitle(localizer["manage.UserDetail", user.name]);

        builder.AddOption(new TextMenuOption(localizer["manage.SteamId", user.account_id]));
        builder.AddOption(new TextMenuOption(localizer["manage.Group", user.group]));
        builder.AddOption(new TextMenuOption(localizer["manage.ExpiresLabel", expiresText]));

        var changeGroupOption = new ButtonMenuOption(localizer["manage.ChangeGroup"]);
        changeGroupOption.Click += async (sender, args) =>
        {
            core.Scheduler.NextTick(() => OpenChangeGroupMenu(args.Player, user));
            await ValueTask.CompletedTask;
        };
        builder.AddOption(changeGroupOption);

        var extendOption = new ButtonMenuOption(localizer["manage.ExtendDuration"]);
        extendOption.Click += async (sender, args) =>
        {
            core.Scheduler.NextTick(() => OpenExtendDurationMenu(args.Player, user));
            await ValueTask.CompletedTask;
        };
        builder.AddOption(extendOption);

        var removeOption = new ButtonMenuOption(localizer["manage.RemoveVip"]);
        removeOption.Click += async (sender, args) =>
        {
            core.Scheduler.NextTick(() =>
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await vipService.RemoveVip(user.account_id);
                        core.Scheduler.NextTick(() =>
                        {
                            var loc = core.Translation.GetPlayerLocalizer(args.Player);
                            args.Player.SendMessage(MessageType.Chat, loc["manage.chat.RemovedVip", user.name, user.account_id]);
                        });
                    }
                    catch (Exception ex)
                    {
                        core.Logger.LogError(ex, "[VIPCore] Failed to remove VIP user {SteamId}", user.account_id);
                        var loc = core.Translation.GetPlayerLocalizer(args.Player);
                        core.Scheduler.NextTick(() => args.Player.SendMessage(MessageType.Chat, loc["manage.chat.FailedRemoveVip", ex.Message]));
                    }
                });
            });
            await ValueTask.CompletedTask;
        };
        builder.AddOption(removeOption);

        var menu = builder.Build();
        core.MenusAPI.OpenMenuForPlayer(admin, menu);
    }

    private void OpenChangeGroupMenu(IPlayer admin, User user)
    {
        var localizer = core.Translation.GetPlayerLocalizer(admin);
        var builder = core.MenusAPI.CreateBuilder();
        builder.Design.SetMenuTitle(localizer["manage.ChangeGroupTitle", user.name]);

        foreach (var groupName in groupsConfig.Groups.Keys)
        {
            var group = groupName;
            var isCurrent = group.Equals(user.group, StringComparison.OrdinalIgnoreCase);
            var option = new ButtonMenuOption(isCurrent ? localizer["manage.CurrentGroup", group] : group);
            option.Enabled = !isCurrent;
            option.Click += async (sender, args) =>
            {
                core.Scheduler.NextTick(() =>
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            user.group = group;
                            await userRepository.UpdateUserAsync(user);

                            var target = core.PlayerManager.GetPlayerFromSteamId((ulong)user.account_id);
                            if (target != null)
                            {
                                await vipService.LoadPlayer(target);
                            }

                            core.Scheduler.NextTick(() =>
                            {
                                var loc = core.Translation.GetPlayerLocalizer(args.Player);
                                args.Player.SendMessage(MessageType.Chat, loc["manage.chat.ChangedGroup", user.name, group]);
                            });
                        }
                        catch (Exception ex)
                        {
                            core.Logger.LogError(ex, "[VIPCore] Failed to change group for {SteamId}", user.account_id);
                            var loc = core.Translation.GetPlayerLocalizer(args.Player);
                            core.Scheduler.NextTick(() => args.Player.SendMessage(MessageType.Chat, loc["manage.chat.FailedChangeGroup", ex.Message]));
                        }
                    });
                });
                await ValueTask.CompletedTask;
            };
            builder.AddOption(option);
        }

        var menu = builder.Build();
        core.MenusAPI.OpenMenuForPlayer(admin, menu);
    }

    private void OpenExtendDurationMenu(IPlayer admin, User user)
    {
        var localizer = core.Translation.GetPlayerLocalizer(admin);
        var builder = core.MenusAPI.CreateBuilder();

        var timeModeLabel = localizer[GetTimeModeKey(coreConfig.TimeMode)];
        builder.Design.SetMenuTitle(localizer["manage.ExtendTitle", user.name, timeModeLabel]);

        var timeOptions = GetTimeOptions(coreConfig.TimeMode);

        foreach (var (time, key) in timeOptions)
        {
            var t = time;
            var timeKey = key;
            var label = localizer[timeKey];
            var option = new ButtonMenuOption($"+{label}");
            option.Click += async (sender, args) =>
            {
                core.Scheduler.NextTick(() =>
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            var baseTime = user.expires == 0
                                ? DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                                : Math.Max(user.expires, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

                            var baseOffset = DateTimeOffset.FromUnixTimeSeconds(baseTime);
                            var newExpires = coreConfig.TimeMode switch
                            {
                                1 => baseOffset.AddMinutes(t).ToUnixTimeSeconds(),
                                2 => baseOffset.AddHours(t).ToUnixTimeSeconds(),
                                3 => baseOffset.AddDays(t).ToUnixTimeSeconds(),
                                _ => baseOffset.AddSeconds(t).ToUnixTimeSeconds()
                            };

                            user.expires = newExpires;
                            await userRepository.UpdateUserAsync(user);

                            var target = core.PlayerManager.GetPlayerFromSteamId((ulong)user.account_id);
                            if (target != null)
                            {
                                await vipService.LoadPlayer(target);
                            }

                            var newExpiresText = DateTimeOffset.FromUnixTimeSeconds(newExpires).ToString("yyyy-MM-dd HH:mm");
                            core.Scheduler.NextTick(() =>
                            {
                                var loc = core.Translation.GetPlayerLocalizer(args.Player);
                                var displayLabel = loc[timeKey];
                                args.Player.SendMessage(MessageType.Chat, loc["manage.chat.ExtendedVip", user.name, displayLabel, newExpiresText]);
                            });
                        }
                        catch (Exception ex)
                        {
                            core.Logger.LogError(ex, "[VIPCore] Failed to extend duration for {SteamId}", user.account_id);
                            var loc = core.Translation.GetPlayerLocalizer(args.Player);
                            core.Scheduler.NextTick(() => args.Player.SendMessage(MessageType.Chat, loc["manage.chat.FailedExtend", ex.Message]));
                        }
                    });
                });
                await ValueTask.CompletedTask;
            };
            builder.AddOption(option);
        }

        var makePermanentOption = new ButtonMenuOption(localizer["manage.MakePermanent"]);
        makePermanentOption.Enabled = user.expires != 0;
        makePermanentOption.Click += async (sender, args) =>
        {
            core.Scheduler.NextTick(() =>
            {
                Task.Run(async () =>
                {
                    try
                    {
                        user.expires = 0;
                        await userRepository.UpdateUserAsync(user);

                        var target = core.PlayerManager.GetPlayerFromSteamId((ulong)user.account_id);
                        if (target != null)
                        {
                            await vipService.LoadPlayer(target);
                        }

                        core.Scheduler.NextTick(() =>
                        {
                            var loc = core.Translation.GetPlayerLocalizer(args.Player);
                            args.Player.SendMessage(MessageType.Chat, loc["manage.chat.MadePermanent", user.name]);
                        });
                    }
                    catch (Exception ex)
                    {
                        core.Logger.LogError(ex, "[VIPCore] Failed to make permanent for {SteamId}", user.account_id);
                        var loc = core.Translation.GetPlayerLocalizer(args.Player);
                        core.Scheduler.NextTick(() => args.Player.SendMessage(MessageType.Chat, loc["manage.chat.FailedMakePermanent", ex.Message]));
                    }
                });
            });
            await ValueTask.CompletedTask;
        };
        builder.AddOption(makePermanentOption);

        var menu = builder.Build();
        core.MenusAPI.OpenMenuForPlayer(admin, menu);
    }
}
