using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Menu;
using SwiftlyS2.Shared.Menu.Components;
using SwiftlyS2.Shared.Translation;
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
        var builder = CreateAdminMenu("vipcore.manage", localizer["manage.Title"]);

        var addOption = new ButtonComponent(localizer["manage.AddVipUser"], async ctx =>
        {
            core.Scheduler.NextTick(() => OpenAddVipSelectPlayerMenu(ctx.Player));
            await ValueTask.CompletedTask;
        });
        builder.Add(addOption);

        var manageOption = new ButtonComponent(localizer["manage.ManageVipUsers"], async ctx =>
        {
            core.Scheduler.NextTick(() =>
            {
                Task.Run(async () =>
                {
                    try
                    {
                        var users = await userRepository.GetAllUsersAsync(serverIdentifier.EffectiveServerId);
                        var userList = users.ToList();
                        core.Scheduler.NextTick(() =>
                        {
                            // The classic API skipped invalid players; Core.Menu would attach a session to a slot the admin has left.
                            if (ctx.Player.IsValid)
                                OpenManageGroupsMenu(ctx.Player, userList);
                        });
                    }
                    catch (Exception ex)
                    {
                        core.Logger.LogError(ex, "[VIPCore] Failed to load VIP users for manage menu.");
                        var loc = core.Translation.GetPlayerLocalizer(ctx.Player);
                        core.Scheduler.NextTick(() => ctx.Player.SendMessage(MessageType.Chat, loc["manage.chat.FailedLoadUsers"]));
                    }
                });
            });
            await ValueTask.CompletedTask;
        });
        builder.Add(manageOption);

        OpenAdminMenu(admin, builder);
    }

    private void OpenAddVipSelectPlayerMenu(IPlayer admin)
    {
        var localizer = core.Translation.GetPlayerLocalizer(admin);
        var builder = CreateAdminMenu("vipcore.manage.select-player", localizer["manage.SelectPlayer"]);

        for (var i = 0; i < core.PlayerManager.PlayerCap; i++)
        {
            var player = core.PlayerManager.GetPlayer(i);
            if (player == null || player.IsFakeClient) continue;

            var steamId = player.SteamID;
            var playerName = player.Controller.PlayerName;

            var option = new ButtonComponent($"{playerName} ({steamId})", async ctx =>
            {
                core.Scheduler.NextTick(() => OpenAddVipSelectGroupMenu(ctx.Player, (long)steamId, playerName));
                await ValueTask.CompletedTask;
            });
            builder.Add(option);
        }

        OpenAdminMenu(admin, builder);
    }

    private void OpenAddVipSelectGroupMenu(IPlayer admin, long steamId, string playerName)
    {
        var localizer = core.Translation.GetPlayerLocalizer(admin);
        var builder = CreateAdminMenu("vipcore.manage.select-group", localizer["manage.SelectGroup", playerName]);

        foreach (var groupName in groupsConfig.Groups.Keys)
        {
            var group = groupName;
            var option = new ButtonComponent(group, async ctx =>
            {
                core.Scheduler.NextTick(() => OpenAddVipSelectScopeMenu(ctx.Player, steamId, playerName, group));
                await ValueTask.CompletedTask;
            });
            builder.Add(option);
        }

        OpenAdminMenu(admin, builder);
    }

    private void OpenAddVipSelectScopeMenu(IPlayer admin, long steamId, string playerName, string group)
    {
        var localizer = core.Translation.GetPlayerLocalizer(admin);
        var builder = CreateAdminMenu("vipcore.manage.select-scope", localizer["manage.SelectScope", playerName]);

        foreach (var global in new[] { false, true })
        {
            var option = new ButtonComponent(localizer[global ? "manage.Scope.Global" : "manage.Scope.Local"], async ctx =>
            {
                core.Scheduler.NextTick(() => OpenAddVipSelectTimeMenu(ctx.Player, steamId, playerName, group, global));
                await ValueTask.CompletedTask;
            });
            builder.Add(option);
        }

        OpenAdminMenu(admin, builder);
    }

    private void OpenAddVipSelectTimeMenu(IPlayer admin, long steamId, string playerName, string group, bool global)
    {
        var localizer = core.Translation.GetPlayerLocalizer(admin);
        var timeModeLabel = localizer[GetTimeModeKey(coreConfig.TimeMode)];
        var builder = CreateAdminMenu("vipcore.manage.select-duration", localizer["manage.SelectDuration", timeModeLabel, playerName]);

        var timeOptions = GetTimeOptions(coreConfig.TimeMode);

        foreach (var (time, key) in timeOptions)
        {
            var t = time;
            var timeKey = key;
            var label = localizer[timeKey];
            var option = new ButtonComponent(label, async ctx =>
            {
                core.Scheduler.NextTick(() =>
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            await vipService.AddVip(steamId, playerName, group, t, global);

                            core.Scheduler.NextTick(() =>
                            {
                                var loc = core.Translation.GetPlayerLocalizer(ctx.Player);
                                var displayLabel = loc[timeKey];
                                ctx.Player.SendMessage(MessageType.Chat, loc["manage.chat.AddedVip", playerName, steamId, $"{group} ({loc[global ? "manage.Scope.Global" : "manage.Scope.Local"]})", displayLabel]);

                                var target = core.PlayerManager.GetPlayerFromSteamId((ulong)VipService.ToSteamId64(steamId));
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
                            var loc = core.Translation.GetPlayerLocalizer(ctx.Player);
                            core.Scheduler.NextTick(() => ctx.Player.SendMessage(MessageType.Chat, loc["manage.chat.FailedAddVip", ex.Message]));
                        }
                    });
                    ctx.Session.Close();
                });
                await ValueTask.CompletedTask;
            });
            builder.Add(option);
        }

        var permanentOption = new ButtonComponent(localizer["manage.Permanent"], async ctx =>
        {
            core.Scheduler.NextTick(() =>
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await vipService.AddVip(steamId, playerName, group, 0, global);

                        core.Scheduler.NextTick(() =>
                        {
                            var loc = core.Translation.GetPlayerLocalizer(ctx.Player);
                            ctx.Player.SendMessage(MessageType.Chat, loc["manage.chat.AddedVipPermanent", playerName, steamId, $"{group} ({loc[global ? "manage.Scope.Global" : "manage.Scope.Local"]})"]);

                            var target = core.PlayerManager.GetPlayerFromSteamId((ulong)VipService.ToSteamId64(steamId));
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
                        var loc = core.Translation.GetPlayerLocalizer(ctx.Player);
                        core.Scheduler.NextTick(() => ctx.Player.SendMessage(MessageType.Chat, loc["manage.chat.FailedAddVip", ex.Message]));
                    }
                });
                ctx.Session.Close();
            });
            await ValueTask.CompletedTask;
        });
        builder.Add(permanentOption);

        OpenAdminMenu(admin, builder);
    }

    private void OpenManageGroupsMenu(IPlayer admin, List<User> users)
    {
        var localizer = core.Translation.GetPlayerLocalizer(admin);
        var builder = CreateAdminMenu("vipcore.manage.groups", localizer["manage.VipGroups"]);

        foreach (var groupName in groupsConfig.Groups.Keys)
        {
            var group = groupName;
            var groupUsers = users.Where(u => string.Equals(u.group, group, StringComparison.OrdinalIgnoreCase)).ToList();
            var playerCount = groupUsers.Select(u => VipService.ToSteamId64(u.account_id)).Distinct().Count();
            var option = new ButtonComponent(localizer["manage.GroupEntry", group, playerCount], async ctx =>
            {
                core.Scheduler.NextTick(() => OpenManageUsersListMenu(ctx.Player, groupUsers));
                await ValueTask.CompletedTask;
            });
            builder.Add(option);
        }

        OpenAdminMenu(admin, builder);
    }

    private void OpenManageUsersListMenu(IPlayer admin, List<User> users)
    {
        var localizer = core.Translation.GetPlayerLocalizer(admin);
        var builder = CreateAdminMenu("vipcore.manage.users", localizer["manage.ManageUsersList"]);

        if (users.Count == 0)
        {
            builder.Add(new TextComponent(localizer["manage.NoVipUsers"]));
        }
        else
        {
            var grouped = users.GroupBy(u => VipService.ToSteamId64(u.account_id)).ToList();
            foreach (var playerGroup in grouped)
            {
                var playerUsers = playerGroup.ToList();
                var first = playerUsers.First();
                var groupNames = string.Join(", ", playerUsers.Select(u => $"{u.group} ({localizer[u.sid == 0 ? "manage.Scope.Global" : "manage.Scope.Local"]})"));
                var option = new ButtonComponent(localizer["manage.UserEntry", first.name, VipService.ToSteamId64(first.account_id), groupNames], async ctx =>
                {
                    core.Scheduler.NextTick(() => OpenUserManageMenu(ctx.Player, playerUsers));
                    await ValueTask.CompletedTask;
                });
                builder.Add(option);
            }
        }

        OpenAdminMenu(admin, builder);
    }

    private void OpenUserManageMenu(IPlayer admin, List<User> playerUsers)
    {
        var localizer = core.Translation.GetPlayerLocalizer(admin);
        var first = playerUsers.First();
        var builder = CreateAdminMenu("vipcore.manage.user", localizer["manage.UserDetail", first.name]);

        builder.Add(new TextComponent(localizer["manage.SteamId", VipService.ToSteamId64(first.account_id)]));

        foreach (var u in playerUsers)
        {
            var userEntry = u;
            var expiresText = userEntry.expires == 0 ? localizer["manage.Permanent"] : DateTimeOffset.FromUnixTimeSeconds(userEntry.expires).ToString("yyyy-MM-dd HH:mm");
            var groupOption = new ButtonComponent(localizer["manage.Group", $"{userEntry.group} ({localizer[userEntry.sid == 0 ? "manage.Scope.Global" : "manage.Scope.Local"]})"], async ctx =>
            {
                core.Scheduler.NextTick(() => OpenGroupManageMenu(ctx.Player, userEntry, playerUsers));
                await ValueTask.CompletedTask;
            });
            groupOption.Comment = localizer["manage.Expires", expiresText];
            builder.Add(groupOption);
        }

        var addGroupOption = new ButtonComponent(localizer["manage.AddGroup"], async ctx =>
        {
            core.Scheduler.NextTick(() => OpenAddGroupMenu(ctx.Player, first.account_id, first.name));
            await ValueTask.CompletedTask;
        });
        builder.Add(addGroupOption);

        foreach (var global in new[] { false, true })
        {
            if (!playerUsers.Any(u => global ? u.sid == 0 : u.sid == serverIdentifier.ServerId)) continue;
            var removeAllOption = new ButtonComponent(localizer[global ? "manage.RemoveAllGlobalVip" : "manage.RemoveAllVip"], async ctx =>
            {
                core.Scheduler.NextTick(() =>
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            await vipService.RemoveVip(first.account_id, global);
                            core.Scheduler.NextTick(() =>
                            {
                                var target = core.PlayerManager.GetPlayerFromSteamId((ulong)VipService.ToSteamId64(first.account_id));
                                if (target != null)
                                {
                                    Task.Run(async () =>
                                    {
                                        try { await vipService.LoadPlayer(target); }
                                        catch (Exception ex) { core.Logger.LogError(ex, "[VIPCore] Failed to reload VIP player {SteamId}", first.account_id); }
                                    });
                                }
                                var loc = core.Translation.GetPlayerLocalizer(ctx.Player);
                                ctx.Player.SendMessage(MessageType.Chat, loc["manage.chat.RemovedVip", first.name, first.account_id]);
                            });
                        }
                        catch (Exception ex)
                        {
                            core.Logger.LogError(ex, "[VIPCore] Failed to remove VIP user {SteamId}", first.account_id);
                            core.Scheduler.NextTick(() =>
                            {
                                var loc = core.Translation.GetPlayerLocalizer(ctx.Player);
                                ctx.Player.SendMessage(MessageType.Chat, loc["manage.chat.FailedRemoveVip", ex.Message]);
                            });
                        }
                    });
                    ctx.Session.Close();
                });
                await ValueTask.CompletedTask;
            });
            builder.Add(removeAllOption);
        }

        OpenAdminMenu(admin, builder);
    }

    private void OpenGroupManageMenu(IPlayer admin, User user, List<User> allPlayerGroups)
    {
        var localizer = core.Translation.GetPlayerLocalizer(admin);
        var expiresText = user.expires == 0 ? localizer["manage.Permanent"] : DateTimeOffset.FromUnixTimeSeconds(user.expires).ToString("yyyy-MM-dd HH:mm");
        var builder = CreateAdminMenu("vipcore.manage.user-group", localizer["manage.UserDetail", $"{user.name} - {user.group}"]);

        builder.Add(new TextComponent(localizer["manage.Group", $"{user.group} ({localizer[user.sid == 0 ? "manage.Scope.Global" : "manage.Scope.Local"]})"]));
        builder.Add(new TextComponent(localizer["manage.ExpiresLabel", expiresText]));

        var extendOption = new ButtonComponent(localizer["manage.ExtendDuration"], async ctx =>
        {
            core.Scheduler.NextTick(() => OpenExtendDurationMenu(ctx.Player, user));
            await ValueTask.CompletedTask;
        });
        builder.Add(extendOption);

        var removeOption = new ButtonComponent(localizer["manage.RemoveVip"], async ctx =>
        {
            core.Scheduler.NextTick(() =>
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await vipService.RemoveVipGroup(user.account_id, user.group, user.sid);

                        var target = core.PlayerManager.GetPlayerFromSteamId((ulong)VipService.ToSteamId64(user.account_id));
                        if (target != null)
                        {
                            await vipService.LoadPlayer(target);
                        }

                        core.Scheduler.NextTick(() =>
                        {
                            var loc = core.Translation.GetPlayerLocalizer(ctx.Player);
                            ctx.Player.SendMessage(MessageType.Chat, loc["manage.chat.RemovedVip", user.name, user.account_id]);
                        });
                    }
                    catch (Exception ex)
                    {
                        core.Logger.LogError(ex, "[VIPCore] Failed to remove VIP group {Group} for {SteamId}", user.group, user.account_id);
                        var loc = core.Translation.GetPlayerLocalizer(ctx.Player);
                        core.Scheduler.NextTick(() => ctx.Player.SendMessage(MessageType.Chat, loc["manage.chat.FailedRemoveVip", ex.Message]));
                    }
                });
                ctx.Session.Close();
            });
            await ValueTask.CompletedTask;
        });
        builder.Add(removeOption);

        OpenAdminMenu(admin, builder);
    }

    private void OpenAddGroupMenu(IPlayer admin, long accountId, string playerName)
    {
        var localizer = core.Translation.GetPlayerLocalizer(admin);
        var builder = CreateAdminMenu("vipcore.manage.select-group", localizer["manage.SelectGroup", playerName]);

        foreach (var groupName in groupsConfig.Groups.Keys)
        {
            var group = groupName;
            var option = new ButtonComponent(group, async ctx =>
            {
                core.Scheduler.NextTick(() => OpenAddVipSelectScopeMenu(ctx.Player, accountId, playerName, group));
                await ValueTask.CompletedTask;
            });
            builder.Add(option);
        }

        OpenAdminMenu(admin, builder);
    }

    private void OpenExtendDurationMenu(IPlayer admin, User user)
    {
        var localizer = core.Translation.GetPlayerLocalizer(admin);
        var timeModeLabel = localizer[GetTimeModeKey(coreConfig.TimeMode)];
        var builder = CreateAdminMenu("vipcore.manage.extend", localizer["manage.ExtendTitle", user.name, timeModeLabel]);

        var timeOptions = GetTimeOptions(coreConfig.TimeMode);

        foreach (var (time, key) in timeOptions)
        {
            var t = time;
            var timeKey = key;
            var label = localizer[timeKey];
            var option = new ButtonComponent($"+{label}", async ctx =>
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

                            var target = core.PlayerManager.GetPlayerFromSteamId((ulong)VipService.ToSteamId64(user.account_id));
                            if (target != null)
                            {
                                await vipService.LoadPlayer(target);
                            }

                            var newExpiresText = DateTimeOffset.FromUnixTimeSeconds(newExpires).ToString("yyyy-MM-dd HH:mm");
                            core.Scheduler.NextTick(() =>
                            {
                                var loc = core.Translation.GetPlayerLocalizer(ctx.Player);
                                var displayLabel = loc[timeKey];
                                ctx.Player.SendMessage(MessageType.Chat, loc["manage.chat.ExtendedVip", user.name, displayLabel, newExpiresText]);
                            });
                        }
                        catch (Exception ex)
                        {
                            core.Logger.LogError(ex, "[VIPCore] Failed to extend duration for {SteamId}", user.account_id);
                            var loc = core.Translation.GetPlayerLocalizer(ctx.Player);
                            core.Scheduler.NextTick(() => ctx.Player.SendMessage(MessageType.Chat, loc["manage.chat.FailedExtend", ex.Message]));
                        }
                    });
                    ctx.Session.Close();
                });
                await ValueTask.CompletedTask;
            });
            builder.Add(option);
        }

        var makePermanentOption = new ButtonComponent(localizer["manage.MakePermanent"], async ctx =>
        {
            core.Scheduler.NextTick(() =>
            {
                Task.Run(async () =>
                {
                    try
                    {
                        user.expires = 0;
                        await userRepository.UpdateUserAsync(user);

                        var target = core.PlayerManager.GetPlayerFromSteamId((ulong)VipService.ToSteamId64(user.account_id));
                        if (target != null)
                        {
                            await vipService.LoadPlayer(target);
                        }

                        core.Scheduler.NextTick(() =>
                        {
                            var loc = core.Translation.GetPlayerLocalizer(ctx.Player);
                            ctx.Player.SendMessage(MessageType.Chat, loc["manage.chat.MadePermanent", user.name]);
                        });
                    }
                    catch (Exception ex)
                    {
                        core.Logger.LogError(ex, "[VIPCore] Failed to make permanent for {SteamId}", user.account_id);
                        var loc = core.Translation.GetPlayerLocalizer(ctx.Player);
                        core.Scheduler.NextTick(() => ctx.Player.SendMessage(MessageType.Chat, loc["manage.chat.FailedMakePermanent", ex.Message]));
                    }
                });
                ctx.Session.Close();
            });
            await ValueTask.CompletedTask;
        });
        makePermanentOption.Enabled = user.expires != 0;
        builder.Add(makePermanentOption);

        OpenAdminMenu(admin, builder);
    }

    private IMenuBuilder CreateAdminMenu(string id, string title)
    {
        return core.Menu.CreateMenu(id).AddHeader(new TitleComponent(title) { Overflow = MenuTextOverflow.None });
    }

    private void OpenAdminMenu(IPlayer admin, IMenuBuilder builder)
    {
        var menu = builder.Build().WithDefaultOption();
        if (coreConfig.FreezeAdminMenu)
        {
            menu.FreezeWhileOpen(core);
        }

        menu.Open(admin);
    }
}