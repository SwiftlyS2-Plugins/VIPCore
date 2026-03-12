using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.Translation;
using System.Text.Json.Serialization;
using VIPCore.Contract;

namespace VIP_Test;

[PluginMetadata(Id = "VIP_Test", Version = "1.0.0", Name = "VIP Test", Author = "aga", Description = "Gives players a timed trial VIP.")]
public class VIP_Test : BasePlugin
{
    private const string CookieCount = "vip_test_count";
    private const string CookieCooldown = "vip_test_cooldown";
    private const string CookieActiveEnd = "vip_test_active_end";

    private IVipCoreApiV1? _vipApi;
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

        try
        {
            if (interfaceManager.HasSharedInterface("VIPCore.Api.v1"))
                _vipApi = interfaceManager.GetSharedInterface<IVipCoreApiV1>("VIPCore.Api.v1");
        }
        catch (Exception ex)
        {
            Core.Logger.LogWarning(ex, "[VIP_Test] Failed to resolve VIPCore.Api.v1.");
        }
    }

    public override void Load(bool hotReload)
    {
        _config = LoadConfig();

        Core.Command.RegisterCommand("viptest", OnCommandVipTest);

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

        var vipTestCount = api.GetPlayerCookie<int>(player, CookieCount);
        var cooldownEndTime = api.GetPlayerCookie<long>(player, CookieCooldown);
        var activeEndTime = api.GetPlayerCookie<long>(player, CookieActiveEnd);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Check if trial VIP is actively running
        if (activeEndTime > now)
        {
            var activeTime = DateTimeOffset.FromUnixTimeSeconds(activeEndTime) - DateTimeOffset.UtcNow;
            var activeTimeFormatted = $"{(activeTime.Days == 0 ? "" : $"{activeTime.Days}d ")}{activeTime.Hours:D2}:{activeTime.Minutes:D2}:{activeTime.Seconds:D2}".Trim();
            
            var localizer = Core.Translation.GetPlayerLocalizer(player);
            player.SendMessage(MessageType.Chat, localizer["viptest.CurrentlyActive", activeTimeFormatted]);
            return;
        }

        // Check if user already has VIP from some other source
        if (api.IsClientVip(player))
        {
            var localizer = Core.Translation.GetPlayerLocalizer(player);
            player.SendMessage(MessageType.Chat, localizer["vip.AlreadyVipPrivileges"]);
            return;
        }

        if (vipTestCount >= config.VipTestCount)
        {
            var localizer = Core.Translation.GetPlayerLocalizer(player);
            player.SendMessage(MessageType.Chat, localizer["viptest.YouCanNoLongerTakeTheVip"]);
            return;
        }

        if (cooldownEndTime > now)
        {
            var time = DateTimeOffset.FromUnixTimeSeconds(cooldownEndTime) - DateTimeOffset.UtcNow;
            var timeRemainingFormatted =
                $"{(time.Days == 0 ? "" : $"{time.Days}d ")}{time.Hours:D2}:{time.Minutes:D2}:{time.Seconds:D2}".Trim();

            var localizer = Core.Translation.GetPlayerLocalizer(player);
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

        var loc = Core.Translation.GetPlayerLocalizer(player);
        player.SendMessage(MessageType.Chat, loc["viptest.SuccessfullyPassed", formattedTime]);
        api.GiveClientVip(player, config.VipTestGroup, config.VipTestDuration);
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