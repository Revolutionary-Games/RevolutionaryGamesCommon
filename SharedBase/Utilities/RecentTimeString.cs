namespace SharedBase.Utilities;

using System;
using System.Globalization;

/// <summary>
///   Formats recent times in shorter format
/// </summary>
public static class RecentTimeString
{
    private static readonly TimeSpan FutureTimeCutoff = -TimeSpan.FromHours(1);

    public static string FormatRecentTimeInLocalTime(DateTime utcTime, bool includeSeconds = true,
        TimeSpan? shortDisplayCutoff = null)
    {
        return utcTime.ToLocalTime().ToString(GetFormatString(utcTime, includeSeconds, shortDisplayCutoff),
            CultureInfo.CurrentCulture);
    }

    public static string GetFormatString(DateTime utcTime, bool includeSeconds = true,
        TimeSpan? shortDisplayCutoff = null)
    {
        shortDisplayCutoff ??= TimeSpan.FromHours(18);

        var now = DateTime.UtcNow;

        // If in the future more than an hour, show full time
        if (now - utcTime < FutureTimeCutoff)
        {
            return "G";
        }

        if (now - utcTime < shortDisplayCutoff)
        {
            return includeSeconds ? "T" : "t";
        }

        return "G";
    }
}
