namespace SharedBase.Utilities;

using System;
using System.Globalization;

public static class ValuePrintHelpers
{
    public static string BytesToMiB(this float number, int decimals = 2, bool suffix = true,
        bool alwaysShowDecimals = false)
    {
        var asMib = Math.Round(number / GlobalConstants.MEBIBYTE, decimals);

        string result;

        if (alwaysShowDecimals)
        {
            result = asMib.ToString($"N{decimals}", CultureInfo.CurrentCulture);
        }
        else
        {
            result = asMib.ToString(CultureInfo.CurrentCulture);
        }

        if (!suffix)
            return result;

        return result + " MiB";
    }

    public static string BytesToMiB(this long number, int decimals = 2, bool suffix = true,
        bool alwaysShowDecimals = false)
    {
        return ((float)number).BytesToMiB(decimals, suffix, alwaysShowDecimals);
    }

    public static string BytesToMiB(this int number, int decimals = 2, bool suffix = true,
        bool alwaysShowDecimals = false)
    {
        return ((float)number).BytesToMiB(decimals, suffix, alwaysShowDecimals);
    }
}
