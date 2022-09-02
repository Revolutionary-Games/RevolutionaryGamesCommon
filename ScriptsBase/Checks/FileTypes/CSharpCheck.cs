namespace ScriptsBase.Checks.FileTypes;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

public class CSharpCheck : FileCheck
{
    /// <summary>
    ///   A bit of extra margin for razor files to have longer lines
    /// </summary>
    public const int MAX_LINE_LENGTH_RAZOR = 140;

    public static readonly Regex MissingFloatDecimalPoint = new(@"(?<![\d.])[^.]\d+f\b");
    public static readonly Regex IncorrectFloatSuffixCase = new(@"^\d+F\W");

    private const string RAZOR_EXTENSION = ".razor";

    private readonly int defaultMaxLength;
    private readonly int maxRazorLength;

    public CSharpCheck(int maxLength = LineCharacterHelpers.MAX_LINE_LENGTH,
        int maxRazorLength = MAX_LINE_LENGTH_RAZOR) : base(".cs", RAZOR_EXTENSION)
    {
        defaultMaxLength = maxLength;
        this.maxRazorLength = maxRazorLength;
    }

    public override async IAsyncEnumerable<string> Handle(string path)
    {
        var maxLength = path.EndsWith(RAZOR_EXTENSION) ? maxRazorLength : defaultMaxLength;
        bool checkingLength = true;

        bool windows = OperatingSystem.IsWindows();

        // It seems File.ReadLines cannot be used here as that doesn't give us the line separators
        var rawData = await File.ReadAllBytesAsync(path);

        var text = Encoding.UTF8.GetString(rawData);

        int lineNumber = 0;

        // TODO: is it too bad here to have all of the split strings in memory at once
        foreach (var line in text.Split('\n'))
        {
            ++lineNumber;

            LineCharacterHelpers.HandleLineLengthCheckControlComments(line, ref checkingLength);

            var tabError = LineCharacterHelpers.CheckLineForTab(line, lineNumber);
            if (tabError != null)
                yield return tabError;

            bool endsWithCarriageReturn = line.EndsWith("\r");

            if (!windows && endsWithCarriageReturn)
            {
                yield return $"Line {lineNumber} contains a windows style line ending (CR LF)";
            }

            var lengthError =
                LineCharacterHelpers.CheckLineForBeingTooLong(line, lineNumber, checkingLength, maxLength);
            if (lengthError != null)
                yield return lengthError;

            var match = MissingFloatDecimalPoint.Match(line);

            if (match.Success)
            {
                yield return
                    $"Line {lineNumber} contains an invalid float format (missing decimal point). " +
                    $"{match.Groups[0].Value}";
            }

            match = IncorrectFloatSuffixCase.Match(line);

            if (match.Success)
            {
                yield return
                    $"Line {lineNumber} contains an uppercase float suffix. {match.Groups[0].Value}";
            }
        }
    }
}
