namespace SharedBase.Utilities;

using System;
using DiffMatchPatch;

public class DiffGenerator
{
    private readonly DiffMatchPatch diffBuilder =
        new(1.5f, 32, 4, 0.5f, 1000, 32, 0.5f, 4);

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

        string encodedDiff;
        lock (diffBuilder)
        {
            var diffs = diffBuilder.DiffMain(oldText, newText);

            // No clue what this does
            // diffBuilder.DiffCleanupSemantic(diffs);

            // We can't encode as JSON, so we have to encode as text.
            // I really wanted to use a reusable string builder, however, the encoding is so complex that this needs
            // to be done like this
            encodedDiff = diffBuilder.DiffToDelta(diffs);
        }

        return new DiffData(encodedDiff);
    }

    /// <summary>
    ///   Applies a diff to a piece of text
    /// </summary>
    /// <param name="original">Text to apply the diff to</param>
    /// <param name="diff">Diff data to apply, if empty won't do anything to the text</param>
    /// <returns>A string containing the result</returns>
    /// <exception cref="ArgumentException">If the diff data is malformed</exception>
    public string ApplyDiff(string original, DiffData diff)
    {
        if (diff.Empty || string.IsNullOrEmpty(diff.DiffDeltaRaw))
            return original;

        string result;
        lock (diffBuilder)
        {
            var diffs = diffBuilder.DiffFromDelta(original, diff.DiffDeltaRaw);

            result = diffBuilder.DiffText2(diffs);
        }

        return result;
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
}
