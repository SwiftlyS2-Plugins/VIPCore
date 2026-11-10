using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.Translation;
using System.Text.Json.Serialization;
using VIPCore.Contract;
using Cookies.Contract;

namespace VIP_Test;

[PluginMetadata(Id = "VIP_Test", Version = "1.0.1", Name = "VIP Test", Author = "aga", Description = "Gives players a timed trial VIP.")]
public class VIP_Test : BasePlugin
{
    private const string CookieCount = "vip_test_count";
    private const string CookieCooldown = "vip_test_cooldown";
    private const string CookieActiveEnd = "vip_test_active_end";

    private IVipCoreApiV1? _vipApi;
    private IPlayerCookiesAPIv1? _cookiesApi;
    private VipTestConfig? _config;

    public VIP_Test(ISwiftlyCore core) : base(core)
    {
    }

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
    {
    }

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        _vipApi = null;
        _cookiesApi = null;

        try
        {
            if (interfaceManager.HasSharedInterface("VIPCore.Api.v1"))
                _vipApi = interfaceManager.GetSharedInterface<IVipCoreApiV1>("VIPCore.Api.v1");
        }
        catch (Exception ex)
        {
            Core.Logger.LogWarning(ex, "[VIP_Test] Failed to resolve VIPCore.Api.v1.");
        }

        try
        {
            if (interfaceManager.HasSharedInterface("Cookies.Player.v1"))
                _cookiesApi = interfaceManager.GetSharedInterface<IPlayerCookiesAPIv1>("Cookies.Player.v1");
        }
        catch (Exception ex)
        {
            Core.Logger.LogWarning(ex, "[VIP_Test] Failed to resolve Cookies.Player.v1.");
        }
    }

    public override void Load(bool hotReload)
    {
        _config = LoadConfig();

        Core.Command.RegisterCommand("viptest", OnCommandVipTest);
        Core.Command.RegisterCommand("viptest_reset", OnCommandVipTestReset, permission: "viptest.reset");

        Core.Logger.LogInformation("[VIP_Test] Plugin loaded.");
    }

    public override void Unload()
    {
        Core.Logger.LogInformation("[VIP_Test] Plugin unloaded.");
    }

    private void OnCommandVipTest(ICommandContext context)
    {
        var player = context.Sender;
        if (player == null || player.IsFakeClient) return;

        var config = _config;
        if (config == null) return;

        if (!config.VipTestEnabled) return;

        var api = _vipApi;
        if (api == null) return;

        var localizer = Core.Translation.GetPlayerLocalizer(player);

        var vipTestCount = api.GetPlayerCookie<int>(player, CookieCount);
        var cooldownEndTime = api.GetPlayerCookie<long>(player, CookieCooldown);
        var activeEndTime = api.GetPlayerCookie<long>(player, CookieActiveEnd);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Check if trial VIP is actively running
        if (activeEndTime > now)
        {
            var activeTime = DateTimeOffset.FromUnixTimeSeconds(activeEndTime) - DateTimeOffset.UtcNow;
            var activeTimeFormatted = $"{(activeTime.Days == 0 ? "" : $"{activeTime.Days}d ")}{activeTime.Hours:D2}:{activeTime.Minutes:D2}:{activeTime.Seconds:D2}".Trim();
            
            player.SendMessage(MessageType.Chat, localizer["viptest.CurrentlyActive", activeTimeFormatted]);
            return;
        }

        // Check if user already has VIP from some other source
        if (api.IsClientVip(player))
        {
            player.SendMessage(MessageType.Chat, localizer["vip.AlreadyVipPrivileges"]);
            return;
        }

        if (vipTestCount >= config.VipTestCount)
        {
            player.SendMessage(MessageType.Chat, localizer["viptest.YouCanNoLongerTakeTheVip"]);
            return;
        }

        if (cooldownEndTime > now)
        {
            var time = DateTimeOffset.FromUnixTimeSeconds(cooldownEndTime) - DateTimeOffset.UtcNow;
            var timeRemainingFormatted =
                $"{(time.Days == 0 ? "" : $"{time.Days}d ")}{time.Hours:D2}:{time.Minutes:D2}:{time.Seconds:D2}".Trim();

            player.SendMessage(MessageType.Chat, localizer["viptest.RetakenThrough", timeRemainingFormatted]);
            return;
        }

        var newCooldownEndTime = DateTimeOffset.UtcNow.AddSeconds(config.VipTestCooldown).ToUnixTimeSeconds();
        var durationEndTime = DateTimeOffset.UtcNow.AddSeconds(config.VipTestDuration).ToUnixTimeSeconds();

        api.SetPlayerCookie(player, CookieCount, vipTestCount + 1);
        api.SetPlayerCookie(player, CookieCooldown, newCooldownEndTime);
        api.SetPlayerCookie(player, CookieActiveEnd, durationEndTime);

        var timeRemaining = DateTimeOffset.FromUnixTimeSeconds(durationEndTime) - DateTimeOffset.UtcNow;
        var formattedTime = timeRemaining.ToString(timeRemaining.Hours > 0 ? @"h\:mm\:ss" : @"m\:ss");

        context.Reply(localizer["viptest.SuccessfullyPassed", formattedTime]);
        api.GiveClientVip(player, config.VipTestGroup, TimeSpan.FromSeconds(config.VipTestDuration));
    }

    private void OnCommandVipTestReset(ICommandContext context)
    {
        var player = context.Sender;
        
        var cookiesApi = _cookiesApi;
        if (cookiesApi == null)
        {
            if (player != null)
                context.Reply(Core.Translation.GetPlayerLocalizer(player)["viptest.CookiesApiMissing"]);
            else
                context.Reply("Cookies API not available. Cannot reset VIP test counts.");
            return;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var resetCount = 0;

        try
        {
            var connection = Core.Database.GetConnection("cookies");
            var steamIds = connection.Query<long>($"SELECT SteamId64 FROM PlayerCookies WHERE Data LIKE @pattern",
                new { pattern = $"%\"{CookieCount}\"%" });

            foreach (var steamId in steamIds)
            {
                try
                {
                    var activeEndTime = cookiesApi.Get<long>(steamId, CookieActiveEnd);

                    // If there's an active VIP test running, set count to 1, otherwise reset to 0
                    var newCount = activeEndTime > now ? 1 : 0;

                    cookiesApi.Set(steamId, CookieCount, newCount);
                    resetCount++;
                }
                catch
                {
                    // Skip rows with errors
                }
            }
        }
        catch (Exception ex)
        {
            if (player != null)
                context.Reply(Core.Translation.GetPlayerLocalizer(player)["viptest.DatabaseError", ex.Message]);
            else
                context.Reply($"Failed to query database: {ex.Message}");
            return;
        }

        if (player != null)
            context.Reply(Core.Translation.GetPlayerLocalizer(player)["viptest.ResetSuccess", resetCount]);
        else
            context.Reply($"Reset VIP test count for {resetCount} player(s) (online + offline).");
    }

    private VipTestConfig LoadConfig()
    {
        Core.Configuration
            .InitializeJsonWithModel<VipTestConfig>("config.jsonc", "vip_test")
            .Configure(builder => builder.AddJsonFile("config.jsonc", optional: true, reloadOnChange: true));

        return Core.Configuration.Manager.GetSection("vip_test").Get<VipTestConfig>() ?? new VipTestConfig();
    }
}

public class VipTestConfig
{
    [JsonPropertyName("VipTestEnabled")]
    public bool VipTestEnabled { get; set; } = true;

    [JsonPropertyName("VipTestDuration")]
    public int VipTestDuration { get; set; } = 3600;

    [JsonPropertyName("VipTestCooldown")]
    public int VipTestCooldown { get; set; } = 86400;

    [JsonPropertyName("VipTestGroup")]
    public string VipTestGroup { get; set; } = "group_name";

    [JsonPropertyName("VipTestCount")]
    public int VipTestCount { get; set; } = 2;
}