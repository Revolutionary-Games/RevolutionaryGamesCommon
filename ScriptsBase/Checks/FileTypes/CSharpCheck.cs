namespace ScriptsBase.Checks.FileTypes;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public static class CSharp
{
    public const int MAX_LINE_LENGTH = 120;
    public const string DISABLE_LINE_LENGTH_COMMENT = "LineLengthCheckDisable";
    public const string ENABLE_LINE_LENGTH_COMMENT = "LineLengthCheckEnable";

    public static async IAsyncEnumerable<string> Handle(string path)
    {
        bool checkingLength = true;

        bool windows = OperatingSystem.IsWindows();

        // It seems File.ReadLines cannot be used here as that doesn't give us the line separators
        var rawData = await File.ReadAllBytesAsync(path);

        var text = Encoding.UTF8.GetString(rawData);

        int lineNumber = 0;
        foreach (var line in text.Split('\n'))
        {
            ++lineNumber;

            if (line.Contains(DISABLE_LINE_LENGTH_COMMENT))
                checkingLength = false;
            if (line.Contains(ENABLE_LINE_LENGTH_COMMENT))
                checkingLength = true;

            if (line.Contains("\t"))
            {
                yield return $"Line {lineNumber} contains a tab";
            }

            bool endsWithCarriageReturn = line.EndsWith("\r");

            if (!windows && endsWithCarriageReturn)
            {
                yield return $"Line {lineNumber} contains a windows style line ending (CR LF)";
            }

            var length = line.Length;

            if (windows && endsWithCarriageReturn)
                --length;

            if (length > MAX_LINE_LENGTH && checkingLength)
            {
                yield return $"Line {lineNumber} is too long. {length} > {MAX_LINE_LENGTH}";
            }
        }
    }
}
