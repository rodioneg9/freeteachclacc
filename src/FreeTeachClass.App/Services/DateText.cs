namespace FreeTeachClass.App;

/// <summary>
/// Formats deadlines in short Russian phrases ("сегодня, 23:59", "завтра", …).
/// </summary>
public static class DateText
{
    public static string Format(DateTimeOffset due, DateTimeOffset now)
    {
        var local = due.ToLocalTime();
        var today = now.ToLocalTime().Date;
        var dayDiff = local.Date - today;
        var timeText = local.ToString("HH:mm");

        // dayDiff is a whole number of days (local.Date − today).
        return dayDiff.Days switch
        {
            < 0 => $"просрочено ({local:dd.MM}, {timeText})",
            0 when local.TimeOfDay >= new TimeSpan(23, 59, 0) => "сегодня, 23:59",
            0 => $"сегодня, {timeText}",
            1 => $"завтра, {timeText}",
            _ => $"{local:dd.MM.yyyy}, {timeText}",
        };
    }
}
