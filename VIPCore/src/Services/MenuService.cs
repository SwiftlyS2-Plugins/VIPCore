using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Translation;
using SwiftlyS2.Core.Menus.OptionsBase;
using VIPCore.Contract;
using VIPCore.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VIPCore.Services;

public class MenuService(ISwiftlyCore core, FeatureService featureService, CookieService cookieService, GroupsConfig groupsConfig, IOptionsMonitor<VipConfig> configMonitor)
{
    private VipConfig config => configMonitor.CurrentValue;
    private static string SafeLocalize(ILocalizer localizer, string key)
    {
        try
        {
            return localizer[key];
        }
        catch
        {
            return key;
        }
    }

    public void OpenVipMenu(IPlayer player, VipUser user, int? selectedIndex = null)
    {
        var localizer = core.Translation.GetPlayerLocalizer(player);
        var title = localizer["menu.Title", user.group];

        var builder = core.MenusAPI.CreateBuilder();
        builder.Design.SetMenuTitle(title);
        
        var groupName = groupsConfig.Groups.Keys.FirstOrDefault(k => k.Equals(user.group, StringComparison.OrdinalIgnoreCase));
        if (groupName == null || !groupsConfig.Groups.TryGetValue(groupName, out var groupConfig))
        {
            core.Logger.LogWarning("[VIPCore] Menu: Group '{Group}' not found in config for player {Name}", user.group, player.Controller.PlayerName);
            player.SendMessage(MessageType.Chat, localizer["vip.NoAccess"]);
            return;
        }

        var features = featureService.GetRegisteredFeatures()
            .Where(f => f.FeatureType != FeatureType.Hide)
            .OrderBy(f => f.Key)
            .ToList();

        if (config.VipLogging)
            core.Logger.LogDebug("[VIPCore] Menu: Opening for {Name} (Group: {Group}). Registered features: {Count}", player.Controller.PlayerName, user.group, features.Count);

        var optionIndex = 0;
        foreach (var feature in features)
        {
            var hasConfig = groupConfig.Values.TryGetValue(feature.Key, out var val);
            var valStr = val?.ToString();
            var isEmpty = string.IsNullOrEmpty(valStr);
            
            if (config.VipLogging)
                core.Logger.LogDebug("[VIPCore] Menu: Feature '{Key}' - InConfig: {InConfig}, Val: '{Val}', IsEmpty: {IsEmpty}", feature.Key, hasConfig, valStr ?? "null", isEmpty);

            if (!hasConfig || isEmpty)
                continue;

            if (!user.FeatureStates.TryGetValue(feature.Key, out var state))
            {
                core.Logger.LogWarning("[VIPCore] Menu: Feature '{Key}' state not found in user FeatureStates", feature.Key);
                continue;
            }

            string stateText = "";
            if (feature.FeatureType == FeatureType.Toggle)
            {
                stateText = $" [{localizer[state == FeatureState.Enabled ? "chat.Enabled" : "chat.Disabled"]}]";
            }
            else if (feature.FeatureType == FeatureType.Selectable)
            {
                try
                {
                    var valueKey = feature.Key + ".value";
                    var value = cookieService.GetCookie<int>(player.SteamID, valueKey);
                    stateText = value == 0
                        ? $" [{localizer["chat.Disabled"]}]"
                        : $" [{value}]";
                }
                catch
                {
                    stateText = "";
                }
            }

            var displayName = feature.DisplayNameResolver != null
                ? feature.DisplayNameResolver(player)
                : feature.Key;
            var itemTitle = displayName + stateText;
            var isDisabled = state == FeatureState.NoAccess || featureService.IsFeatureForcedDisabled(feature.Key);

            var option = new ButtonMenuOption(itemTitle) { Enabled = !isDisabled };
            var thisOptionIndex = optionIndex;
            optionIndex++;
            option.Click += async (sender, args) =>
            {
                if (isDisabled) return;

                core.Scheduler.NextTick(() =>
                {
                    if (feature.FeatureType == FeatureType.Toggle)
                    {
                        var newState = state == FeatureState.Enabled ? FeatureState.Disabled : FeatureState.Enabled;
                        user.FeatureStates[feature.Key] = newState;
                        cookieService.SetCookie(player.SteamID, feature.Key, (int)newState);
                        
                        feature.OnSelectItem?.Invoke(args.Player, newState);
                        
                        OpenVipMenu(args.Player, user, thisOptionIndex);
                    }
                    else if (feature.FeatureType == FeatureType.Selectable)
                    {
                        feature.OnSelectItem?.Invoke(args.Player, state);
                        core.Scheduler.NextTick(() => OpenVipMenu(args.Player, user, thisOptionIndex));
                    }
                });
                
                await ValueTask.CompletedTask;
            };

            builder.AddOption(option);
        }

        var menu = builder.Build();
        core.MenusAPI.OpenMenuForPlayer(player, menu);
        if (selectedIndex.HasValue)
        {
            var idx = selectedIndex.Value;
            try
            {
                menu.MoveToOptionIndex(player, idx);
            }
            catch
            {
            }
        }
    }
}
