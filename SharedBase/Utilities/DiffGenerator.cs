namespace SharedBase.Utilities;

using System;
using System.Text;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using TextDiff;
using TextDiff.Models;

public class DiffGenerator
{
    private readonly InlineDiffBuilder diffBuilder = new(new Differ());

    private readonly TextDiffer textDiff = new();

    private readonly StringBuilder lineBuilder = new();

    public static DiffGenerator Default { get; } = new();

    /// <summary>
    ///   Generate a line-based diff by comparing text
    /// </summary>
    /// <param name="oldText">The original version</param>
    /// <param name="newText">The new version</param>
    /// <returns>Diffing result</returns>
    public DiffData Generate(string oldText, string newText)
    {
        // Special case handling
        var specialResult = HandleSpecialCases(oldText, newText);
        if (specialResult != null)
            return specialResult;

        var newLine = "\n";
        if (ShouldUseWindowsLineEndings(newText))
            newLine = "\r\n";

        string fullText;
        lock (diffBuilder)
        {
            var initialDiff = diffBuilder.BuildDiffModel(oldText, newText);

            fullText = ConvertToUnifiedDiff(initialDiff, newLine);
        }

        return new DiffData(fullText)
        {
            PreferWindowsLineEndings = ShouldUseWindowsLineEndings(newText),
        };
    }

    /// <summary>
    ///   Applies a diff to a piece of text
    /// </summary>
    /// <param name="original">Text to apply the diff to</param>
    /// <param name="diff">Diff data to apply, if empty won't do anything to the text</param>
    /// <returns>A string containing the result</returns>
    /// <exception cref="ArgumentException">If the diff data is malformed</exception>
    /// <exception cref="TextDiff.Exceptions.TextDiffException">
    ///   If the diff data doesn't match the text and can't be applied
    /// </exception>
    public string ApplyDiff(string original, DiffData diff)
    {
        if (diff.Empty || string.IsNullOrEmpty(diff.UnifiedDiffText))
            return original;

        ProcessResult result;
        lock (textDiff)
        {
            // TODO: should this use ProcessOptimized if the original is long?
            result = textDiff.Process(original, diff.UnifiedDiffText);
        }

        // If we want Windows line endings, we need to force it here as the library doesn't do that
        if (diff.PreferWindowsLineEndings)
        {
            return result.Text.Replace("\n", "\r\n");
        }

        return result.Text;
    }

    private static DiffData? HandleSpecialCases(string oldText, string newText)
    {
        if (oldText == newText)
            return new DiffData();

        // Special case with other string being empty
        if (oldText.Length < 1 && newText.Length < 1)
        {
            // Both are empty
            return new DiffData();
        }

        return null;
    }

    private static bool ShouldUseWindowsLineEndings(string sampleText)
    {
        return sampleText.Contains("\r\n");
    }

    private string ConvertToUnifiedDiff(DiffPaneModel diffData, string lineSeparator)
    {
        lineBuilder.Clear();

        bool first = true;

        foreach (var line in diffData.Lines)
        {
            switch (line.Type)
            {
                case ChangeType.Unchanged:
                    if (!first)
                        lineBuilder.Append(lineSeparator);

                    lineBuilder.Append(' ');
                    lineBuilder.Append(line.Text);
                    break;

                case ChangeType.Deleted:
                    if (!first)
                        lineBuilder.Append(lineSeparator);

                    lineBuilder.Append('-');
                    lineBuilder.Append(line.Text);
                    break;

                case ChangeType.Inserted:
                    if (!first)
                        lineBuilder.Append(lineSeparator);

                    lineBuilder.Append('+');
                    lineBuilder.Append(line.Text);
                    break;
            }

            first = false;
        }

        return lineBuilder.ToString();
    }
}
