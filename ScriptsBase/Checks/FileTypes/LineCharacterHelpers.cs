namespace ScriptsBase.Checks.FileTypes;

using System;

public static class LineCharacterHelpers
{
    public const int MAX_LINE_LENGTH = 120;

    public const string DISABLE_LINE_LENGTH_COMMENT = "LineLengthCheckDisable";
    public const string ENABLE_LINE_LENGTH_COMMENT = "LineLengthCheckEnable";

    public static string? CheckLineForTab(string line, int lineNumber)
    {
        if (line.Contains('\t'))
        {
            return $"Line {lineNumber} contains a tab";
        }

        return null;
    }

    public static void HandleLineLengthCheckControlComments(string line, ref bool checkingLength)
    {
        if (line.Contains(DISABLE_LINE_LENGTH_COMMENT))
            checkingLength = false;
        if (line.Contains(ENABLE_LINE_LENGTH_COMMENT))
            checkingLength = true;
    }

    public static string? CheckLineForBeingTooLong(string line, int lineNumber, bool checkingLength,
        int maxLength = MAX_LINE_LENGTH)
    {
        if (!checkingLength)
            return null;

        var length = line.Length;

        if (length < maxLength)
            return null;

        // Windows line end grace (in case the line contains the line terminators)
        if (line.EndsWith("\r") && OperatingSystem.IsWindows())
            --length;

        if (length > maxLength)
        {
            return $"Line {lineNumber} is too long. {length} > {maxLength}";
        }

        return null;
    }
}
