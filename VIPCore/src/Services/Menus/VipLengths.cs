namespace VIPCore.Services.Menus;

/// <summary>Grant lengths. <see cref="Length.Time"/> is in TimeMode units: 0 seconds, 1 minutes, 2 hours, 3 days.</summary>
internal static class VipLengths
{
    public const string MonthKey = "manage.Time.1Month";

    public readonly record struct Length(int Time, string LabelKey, TimeSpan Span);

    // Five in every TimeMode: more do not fit the detail column of the VIP users page.
    private static readonly (TimeSpan Span, string LabelKey)[] Steps =
    [
        (TimeSpan.FromDays(1), "manage.Time.1Day"),
        (TimeSpan.FromDays(7), "manage.Time.1Week"),
        (TimeSpan.FromDays(30), MonthKey),
        (TimeSpan.FromDays(90), "manage.Time.3Months"),
        (TimeSpan.FromDays(365), "manage.Time.1Year"),
    ];

    /// <summary>The lengths Grant VIP offers besides Permanent.</summary>
    public static Length[] GrantOptions(int timeMode)
    {
        (int Time, string LabelKey)[] options = timeMode switch
        {
            1 => [(30, "manage.Time.30Minutes"), (60, "manage.Time.1Hour"), (120, "manage.Time.2Hours"), (1440, "manage.Time.1Day"), (10080, "manage.Time.1Week"), (43200, MonthKey)],
            2 => [(1, "manage.Time.1Hour"), (2, "manage.Time.2Hours"), (6, "manage.Time.6Hours"), (12, "manage.Time.12Hours"), (24, "manage.Time.1Day"), (168, "manage.Time.1Week"), (720, MonthKey)],
            3 => [(1, "manage.Time.1Day"), (7, "manage.Time.1Week"), (14, "manage.Time.2Weeks"), (30, MonthKey), (90, "manage.Time.3Months"), (365, "manage.Time.1Year")],
            _ => [(3600, "manage.Time.1Hour"), (86400, "manage.Time.1Day"), (604800, "manage.Time.1Week"), (2592000, MonthKey), (7776000, "manage.Time.3Months"), (31536000, "manage.Time.1Year")]
        };

        return options.Select(option => new Length(option.Time, option.LabelKey, ToSpan(option.Time, timeMode))).ToArray();
    }

    /// <summary>The steps that extend a grant on the VIP users page.</summary>
    public static Length[] ExtendSteps(int timeMode) =>
        Steps.Select(step => new Length(ToTime(step.Span, timeMode), step.LabelKey, step.Span)).ToArray();

    public static TimeSpan ToSpan(int time, int timeMode) => timeMode switch
    {
        1 => TimeSpan.FromMinutes(time),
        2 => TimeSpan.FromHours(time),
        3 => TimeSpan.FromDays(time),
        _ => TimeSpan.FromSeconds(time)
    };

    private static int ToTime(TimeSpan span, int timeMode) => (int)(timeMode switch
    {
        1 => span.TotalMinutes,
        2 => span.TotalHours,
        3 => span.TotalDays,
        _ => span.TotalSeconds
    });
}
