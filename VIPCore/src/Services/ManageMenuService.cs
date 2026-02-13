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
    GroupsConfig groupsConfig,
    ServerIdentifier serverIdentifier)
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
                        var users = await userRepository.GetAllUsersAsync(serverIdentifier.ServerId);
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

            var accountId = player.SteamID;
            var playerName = player.Controller.PlayerName;

            var option = new ButtonMenuOption($"{playerName} ({accountId})");
            option.Click += async (sender, args) =>
            {
                core.Scheduler.NextTick(() => OpenAddVipSelectGroupMenu(args.Player, accountId, playerName));
                await ValueTask.CompletedTask;
            };
            builder.AddOption(option);
        }

        var menu = builder.Build();
        core.MenusAPI.OpenMenuForPlayer(admin, menu);
    }

    private void OpenAddVipSelectGroupMenu(IPlayer admin, long accountId, string playerName)
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
                core.Scheduler.NextTick(() => OpenAddVipSelectTimeMenu(args.Player, accountId, playerName, group));
                await ValueTask.CompletedTask;
            };
            builder.AddOption(option);
        }

        var menu = builder.Build();
        core.MenusAPI.OpenMenuForPlayer(admin, menu);
    }

    private void OpenAddVipSelectTimeMenu(IPlayer admin, long accountId, string playerName, string group)
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
                            await vipService.AddVip(accountId, playerName, group, t);

                            core.Scheduler.NextTick(() =>
                            {
                                var loc = core.Translation.GetPlayerLocalizer(args.Player);
                                var displayLabel = loc[timeKey];
                                args.Player.SendMessage(MessageType.Chat, loc["manage.chat.AddedVip", playerName, accountId, group, displayLabel]);

                                var target = core.PlayerManager.GetPlayerFromSteamId(accountId);
                                if (target != null)
                                {
                                    Task.Run(async () =>
                                    {
                                        try { await vipService.LoadPlayer(target); }
                                        catch (Exception ex) { core.Logger.LogError(ex, "[VIPCore] Failed to load newly added VIP player {SteamId}", accountId); }
                                    });
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            core.Logger.LogError(ex, "[VIPCore] Failed to add VIP user {SteamId}", accountId);
                            var loc = core.Translation.GetPlayerLocalizer(args.Player);
                            core.Scheduler.NextTick(() => args.Player.SendMessage(MessageType.Chat, loc["manage.chat.FailedAddVip", ex.Message]));
                        }
                    });
                    core.MenusAPI.CloseActiveMenu(args.Player);
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
                        await vipService.AddVip(accountId, playerName, group, 0);

                        core.Scheduler.NextTick(() =>
                        {
                            var loc = core.Translation.GetPlayerLocalizer(args.Player);
                            args.Player.SendMessage(MessageType.Chat, loc["manage.chat.AddedVipPermanent", playerName, accountId, group]);

                            var target = core.PlayerManager.GetPlayerFromSteamId(accountId);
                            if (target != null)
                            {
                                Task.Run(async () =>
                                {
                                    try { await vipService.LoadPlayer(target); }
                                    catch (Exception ex) { core.Logger.LogError(ex, "[VIPCore] Failed to load newly added VIP player {SteamId}", accountId); }
                                });
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        core.Logger.LogError(ex, "[VIPCore] Failed to add VIP user {SteamId}", accountId);
                        var loc = core.Translation.GetPlayerLocalizer(args.Player);
                        core.Scheduler.NextTick(() => args.Player.SendMessage(MessageType.Chat, loc["manage.chat.FailedAddVip", ex.Message]));
                    }
                });
                core.MenusAPI.CloseActiveMenu(args.Player);
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
            var grouped = users.GroupBy(u => u.account_id).ToList();
            foreach (var playerGroup in grouped)
            {
                var playerUsers = playerGroup.ToList();
                var first = playerUsers.First();
                var groupNames = string.Join(", ", playerUsers.Select(u => u.group));
                var option = new ButtonMenuOption(localizer["manage.UserEntry", first.name, first.account_id, groupNames]);
                option.Click += async (sender, args) =>
                {
                    core.Scheduler.NextTick(() => OpenUserManageMenu(args.Player, playerUsers));
                    await ValueTask.CompletedTask;
                };
                builder.AddOption(option);
            }
        }

        var menu = builder.Build();
        core.MenusAPI.OpenMenuForPlayer(admin, menu);
    }

    private void OpenUserManageMenu(IPlayer admin, List<User> playerUsers)
    {
        var localizer = core.Translation.GetPlayerLocalizer(admin);
        var builder = core.MenusAPI.CreateBuilder();
        var first = playerUsers.First();

        builder.Design.SetMenuTitle(localizer["manage.UserDetail", first.name]);

        builder.AddOption(new TextMenuOption(localizer["manage.SteamId", first.account_id]));

        foreach (var u in playerUsers)
        {
            var userEntry = u;
            var expiresText = userEntry.expires == 0 ? localizer["manage.Permanent"] : DateTimeOffset.FromUnixTimeSeconds(userEntry.expires).ToString("yyyy-MM-dd HH:mm");
            var groupOption = new ButtonMenuOption(localizer["manage.Group", userEntry.group]);
            groupOption.Comment = localizer["manage.Expires", expiresText];
            groupOption.Click += async (sender, args) =>
            {
                core.Scheduler.NextTick(() => OpenGroupManageMenu(args.Player, userEntry, playerUsers));
                await ValueTask.CompletedTask;
            };
            builder.AddOption(groupOption);
        }

        var addGroupOption = new ButtonMenuOption(localizer["manage.AddGroup"]);
        addGroupOption.Click += async (sender, args) =>
        {
            core.Scheduler.NextTick(() => OpenAddGroupMenu(args.Player, first.account_id, first.name, playerUsers));
            await ValueTask.CompletedTask;
        };
        builder.AddOption(addGroupOption);

        var removeAllOption = new ButtonMenuOption(localizer["manage.RemoveAllVip"]);
        removeAllOption.Click += async (sender, args) =>
        {
            core.Scheduler.NextTick(() =>
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await vipService.RemoveVip((long)first.account_id);
                        core.Scheduler.NextTick(() =>
                        {
                            var loc = core.Translation.GetPlayerLocalizer(args.Player);
                            args.Player.SendMessage(MessageType.Chat, loc["manage.chat.RemovedVip", first.name, first.account_id]);
                        });
                    }
                    catch (Exception ex)
                    {
                        core.Logger.LogError(ex, "[VIPCore] Failed to remove VIP user {SteamId}", first.account_id);
                        var loc = core.Translation.GetPlayerLocalizer(args.Player);
                        core.Scheduler.NextTick(() => args.Player.SendMessage(MessageType.Chat, loc["manage.chat.FailedRemoveVip", ex.Message]));
                    }
                });
                core.MenusAPI.CloseActiveMenu(args.Player);
            });
            await ValueTask.CompletedTask;
        };
        builder.AddOption(removeAllOption);

        var menu = builder.Build();
        core.MenusAPI.OpenMenuForPlayer(admin, menu);
    }

    private void OpenGroupManageMenu(IPlayer admin, User user, List<User> allPlayerGroups)
    {
        var localizer = core.Translation.GetPlayerLocalizer(admin);
        var builder = core.MenusAPI.CreateBuilder();

        var expiresText = user.expires == 0 
            ? localizer["manage.Permanent"] 
            : DateTimeOffset.FromUnixTimeSeconds(user.expires).UtcDateTime.ToString("yyyy-MM-dd HH:mm");
        builder.Design.SetMenuTitle(localizer["manage.UserDetail", $"{user.name} - {user.group}"]);

        builder.AddOption(new TextMenuOption(localizer["manage.Group", user.group]));
        builder.AddOption(new TextMenuOption(localizer["manage.ExpiresLabel", expiresText]));

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
                        await vipService.RemoveVipGroup((long)user.account_id, user.group);

                        var target = core.PlayerManager.GetPlayerFromSteamId(user.account_id);
                        if (target != null)
                        {
                            await vipService.LoadPlayer(target);
                        }

                        core.Scheduler.NextTick(() =>
                        {
                            var loc = core.Translation.GetPlayerLocalizer(args.Player);
                            args.Player.SendMessage(MessageType.Chat, loc["manage.chat.RemovedVip", user.name, user.account_id]);
                        });
                    }
                    catch (Exception ex)
                    {
                        core.Logger.LogError(ex, "[VIPCore] Failed to remove VIP group {Group} for {SteamId}", user.group, user.account_id);
                        var loc = core.Translation.GetPlayerLocalizer(args.Player);
                        core.Scheduler.NextTick(() => args.Player.SendMessage(MessageType.Chat, loc["manage.chat.FailedRemoveVip", ex.Message]));
                    }
                });
                core.MenusAPI.CloseActiveMenu(args.Player);
            });
            await ValueTask.CompletedTask;
        };
        builder.AddOption(removeOption);

        var menu = builder.Build();
        core.MenusAPI.OpenMenuForPlayer(admin, menu);
    }

    private void OpenAddGroupMenu(IPlayer admin, ulong accountId, string playerName, List<User> existingGroups)
    {
        var localizer = core.Translation.GetPlayerLocalizer(admin);
        var builder = core.MenusAPI.CreateBuilder();
        builder.Design.SetMenuTitle(localizer["manage.SelectGroup", playerName]);

        var existingGroupNames = existingGroups.Select(u => u.group).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var groupName in groupsConfig.Groups.Keys)
        {
            var group = groupName;
            var alreadyHas = existingGroupNames.Contains(group);
            var option = new ButtonMenuOption(alreadyHas ? localizer["manage.CurrentGroup", group] : group);
            option.Enabled = !alreadyHas;
            option.Click += async (sender, args) =>
            {
                core.Scheduler.NextTick(() => OpenAddVipSelectTimeMenu(args.Player, accountId, playerName, group));
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
                            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                            var baseTime = user.expires == 0
                                ? DateTimeOffset.UtcNow
                                : (user.expires > nowUnix ? DateTimeOffset.FromUnixTimeSeconds(user.expires) : DateTimeOffset.UtcNow);

                            var newExpires = coreConfig.TimeMode switch
                            {
                                1 => baseTime.AddMinutes(t),
                                2 => baseTime.AddHours(t),
                                3 => baseTime.AddDays(t),
                                _ => baseTime.AddSeconds(t)
                            };

                            user.expires = newExpires.ToUnixTimeSeconds();
                            await userRepository.UpdateUserAsync(user);

                            var target = core.PlayerManager.GetPlayerFromSteamId(user.account_id);
                            if (target != null)
                            {
                                await vipService.LoadPlayer(target);
                            }

                            var newExpiresText = DateTimeOffset.FromUnixTimeSeconds(user.expires).UtcDateTime.ToString("yyyy-MM-dd HH:mm");
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
                    core.MenusAPI.CloseActiveMenu(args.Player);
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

                        var target = core.PlayerManager.GetPlayerFromSteamId(user.account_id);
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
                core.MenusAPI.CloseActiveMenu(args.Player);
            });
            await ValueTask.CompletedTask;
        };
        builder.AddOption(makePermanentOption);

        var menu = builder.Build();
        core.MenusAPI.OpenMenuForPlayer(admin, menu);
    }
}
