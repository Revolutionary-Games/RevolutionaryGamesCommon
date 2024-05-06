namespace SharedBase.Utilities;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class DiffGenerator
{
    /// <summary>
    ///   Special value used when a line references the start of the text
    /// </summary>
    public const string StartLineReference = "(start)";

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

        int lineNumber1 = 1;
        int lineNumber2 = 1;

        int lineStart1 = 0;
        int lineStart2 = 0;

        bool lineEnded1 = false;
        bool lineEnded2 = false;

        int i;
        int j = 0;

        var resultBlocks = new List<DiffData.Block>();

        bool openBlock = false;
        DiffData.Block blockData = default(DiffData.Block);

        // Compare the strings line by line
        for (i = 0; i < oldText.Length; ++i)
        {
            // Process line by line, so just look for the next newline
            if (oldText[i] != '\r' && oldText[i] != '\n')
                continue;

            lineEnded1 = true;
            ++lineNumber1;

            lineEnded2 = LookForLineEnd(ref j, ref lineNumber2, newText);

            // After first difference, record differences until the two texts converge again
            if (openBlock)
            {
                // Setup some extra variables to be able to undo the scan into the future
                int scanJ = j;
                int savedLineStart = lineStart2;
                int savedLineNumber = lineNumber2;

                // Try to look for a matching line in the future were we can start processing lines as equal again
                while (scanJ < newText.Length && !LinesEqual(i, oldText, scanJ, newText))
                {
                    if (lineEnded2)
                    {
                        MoveToNextLine(ref scanJ, out lineStart2, newText);
                    }
                    else
                    {
                        // Kind of a bad state, but we can just break out of here to continue basically normally
                        break;
                    }

                    lineEnded2 = LookForLineEnd(ref scanJ, ref lineNumber2, newText);
                }

                if (LinesEqual(i, oldText, scanJ, newText))
                {
                    // All data from the scan start to before the matching line need to be copied to the diff block
                    int copyJ = j;

                    while (copyJ < scanJ)
                    {
                        int currentCopyStart = copyJ;

                        int dummy = 0;
                        bool end = LookForLineEnd(ref copyJ, ref dummy, newText);

                        blockData.AddedLines ??= [];

                        if (end)
                        {
                            // Don't copy the line end character
                            --copyJ;

                            blockData.AddedLines.Add(newText.Substring(currentCopyStart, copyJ - currentCopyStart + 1));

                            ++copyJ;
                            MoveToNextLine(ref copyJ, out dummy, newText);
                        }
                        else
                        {
                            blockData.AddedLines.Add(newText.Substring(currentCopyStart, copyJ - currentCopyStart + 1));
                        }
                    }

                    if (copyJ < scanJ)
                        throw new Exception("Couldn't match up lines to copy to diff");

                    // No longer diverging
                    resultBlocks.Add(blockData);
                    openBlock = false;

                    // Let text advance to the equal part
                }
                else
                {
                    // Add divergence
                    blockData.AddedLines ??= [];
                    blockData.AddedLines.Add(ReadCurrentLineToStart(j, newText));

                    blockData.DeletedLines ??= [];
                    blockData.DeletedLines.Add(ReadCurrentLineToStart(i, oldText));

                    // Restore things after looking for a matching line

                    lineStart2 = savedLineStart;
                    lineNumber2 = savedLineNumber;
                }
            }
            else
            {
                if (!lineEnded2)
                {
                    // Difference on this line as there is no longer a line end in 2, but there is one in line 1
                    OnLineDifference(i, oldText, j, newText, ref blockData);
                    openBlock = true;
                }
                else
                {
                    openBlock = CheckLineDifference(i, oldText, j, newText, ref blockData);
                }
            }

            MoveToNextLine(ref i, out lineStart1, oldText);
            lineEnded1 = false;

            if (lineEnded2)
            {
                MoveToNextLine(ref j, out lineStart2, newText);
                lineEnded2 = false;
            }
        }

        if (i >= oldText.Length && j >= newText.Length)
        {
            // Reached the end of both texts, no longer anything to process

            if (openBlock)
            {
                // If there is still an open block, there's pending data that needs handling
                throw new NotImplementedException();
            }
        }
        else if (i < oldText.Length)
        {
            // Old text is longer (or there are no newlines)
            if (openBlock)
            {
                throw new NotImplementedException();
            }
            else
            {
                OnLineDifference(oldText.Length - 1, oldText, j, newText, ref blockData);
                openBlock = true;
            }
        }
        else if (j < newText.Length)
        {
            // New text is longer (or there are no newlines)
            if (openBlock)
            {
                // Add content until the end
                while (j < newText.Length)
                {
                    bool end = LookForLineEnd(ref j, ref lineNumber2, newText);

                    blockData.AddedLines ??= [];
                    blockData.AddedLines.Add(ReadCurrentLineToStart(j, newText));

                    if (end)
                        MoveToNextLine(ref j, out lineStart2, newText);
                }
            }
            else
            {
                OnLineDifference(i, oldText, newText.Length - 1, newText, ref blockData);
                openBlock = true;
            }
        }
        else
        {
            throw new Exception("Shouldn't reach here");
        }

        // End the final block
        if (openBlock)
            resultBlocks.Add(blockData);

        if (resultBlocks.Count > 0)
        {
            // Remove last added / removed blank line if both strings end in new lines. This is necessary as the line
            // re-convergence algorithm doesn't work with blank lines.
            if (newText[^1] is '\r' or '\n' && oldText[^1] is '\r' or '\n')
            {
                // Unfortunately need to copy memory here
                var lastBlock = resultBlocks[^1];

                if (lastBlock.AddedLines is { Count: > 0 })
                {
                    if (lastBlock.AddedLines[^1].Length < 1)
                        lastBlock.AddedLines.RemoveAt(lastBlock.AddedLines.Count - 1);
                }

                if (lastBlock.DeletedLines is { Count: > 0 })
                {
                    if (lastBlock.DeletedLines[^1].Length < 1)
                        lastBlock.DeletedLines.RemoveAt(lastBlock.DeletedLines.Count - 1);
                }
            }
        }

        return new DiffData(resultBlocks);
    }

    public StringBuilder ApplyDiff(string original, DiffData diff, StringBuilder? reuseBuilder = null)
    {
        string lineEndings = "\n";

        if (original.Contains("\r\n"))
            lineEndings = "\r\n";

        reuseBuilder ??= new StringBuilder(original.Length);

        throw new NotImplementedException();

        return reuseBuilder;
    }

    /// <summary>
    ///   Checks if lines differ and if so starts a new differences block. Both indexes should be at an end of line
    ///   character or end of the text.
    /// </summary>
    /// <returns>True when there are differences</returns>
    private static bool CheckLineDifference(int endIndex1, string text1, int endIndex2, string text2,
        ref DiffData.Block block)
    {
        if (LinesEqual(endIndex1, text1, endIndex2, text2))
            return false;

        // Lines differ, start a block
        OnLineDifference(endIndex1, text1, endIndex2, text2, ref block);

        return true;
    }

    /// <summary>
    ///   Scans text backwards from the end indexes to determine if two lines are equal
    /// </summary>
    /// <returns>True if equal, false if they differ</returns>
    private static bool LinesEqual(int endIndex1, string text1, int endIndex2, string text2,
        bool adjustBackwardsIfAtNewLine = true)
    {
        if (adjustBackwardsIfAtNewLine)
        {
            if (endIndex1 < text1.Length && text1[endIndex1] is '\r' or '\n')
                --endIndex1;

            if (endIndex2 < text2.Length && text2[endIndex2] is '\r' or '\n')
                --endIndex2;
        }

        if (endIndex1 >= text1.Length || endIndex2 >= text2.Length)
        {
            // Equal if both have ended
            return endIndex1 >= text1.Length && endIndex2 >= text2.Length;
        }

        // Scan backwards until the previous line change or start of the text
        for (; endIndex1 >= 0; --endIndex1)
        {
            // Quit on first differing character
            if (text1[endIndex1] != text2[endIndex2])
                return false;

            // If reached a newline then can stop
            bool newLine1 = text1[endIndex1] is '\r' or '\n';
            bool newLine2 = text2[endIndex2] is '\r' or '\n';

            if (newLine1 || newLine2)
            {
                // Equal only if both reached a newline at the same time
                return newLine1 && newLine2;
            }

            --endIndex2;

            if (endIndex2 < 0)
            {
                // Adjust index 1 as breaking will skip one adjustment of that value, and we have already checked the
                // current value
                --endIndex1;

                break;
            }
        }

        // Only equal at the start if both texts ended
        return endIndex1 < 0 && endIndex2 < 0;
    }

    private static string ReadCurrentLineToStart(int endIndex, string text, bool adjustBackwardsIfAtNewLine = true)
    {
        // Allow taking from index that has gone just off the end of the string
        if (endIndex == text.Length)
        {
            --endIndex;
        }
        else if (adjustBackwardsIfAtNewLine)
        {
            if (endIndex < text.Length && text[endIndex] is '\r' or '\n')
                --endIndex;
        }

        for (int i = endIndex; i >= 0; --i)
        {
            if (text[i] is '\r' or '\n')
            {
                // Copy just before this
                ++i;

                return text.Substring(i, endIndex - i + 1);
            }
        }

        // Copy all the way from string start
        return text.Substring(0, endIndex + 1);
    }

    /// <summary>
    ///   Starts a new block when line differences are found
    /// </summary>
    private static void OnLineDifference(int endIndex1, string text1, int endIndex2, string text2,
        ref DiffData.Block block)
    {
        // TODO: scan backwards for reference lines
        // throw new NotImplementedException();

        // StartLineReference

        // TODO: detect how many instances of text to skip

        // TODO: calculate the approximate distance to previous block
        // block.ExpectedOffset

        // Old text is removed and new line is added at the block
        // Skip if already past the end to handle cases where other text has ended and the other wants to add more text
        if (endIndex2 >= text2.Length)
        {
            block.AddedLines?.Clear();
        }
        else
        {
            block.AddedLines = [ReadCurrentLineToStart(endIndex2, text2)];
        }

        if (endIndex1 >= text1.Length)
        {
            block.DeletedLines?.Clear();
        }
        else
        {
            block.DeletedLines = [ReadCurrentLineToStart(endIndex1, text1)];
        }
    }

    private static void MoveToNextLine(ref int index, out int lineStart, string text)
    {
        // Skip multibyte line ending
        if (text[index] == '\r' && index + 1 < text.Length && text[index] == '\n')
        {
            index += 2;
        }
        else
        {
            ++index;
        }

        lineStart = index;
    }

    private static bool LookForLineEnd(ref int index, ref int lineNumber, string text)
    {
        for (; index < text.Length; ++index)
        {
            if (text[index] == '\r' || text[index] == '\n')
            {
                ++lineNumber;
                return true;
            }
        }

        return false;
    }

    private static DiffData? HandleSpecialCases(string oldText, string newText)
    {
        if (oldText == newText)
            return new DiffData();

        // Special case with other string being empty
        if (oldText.Length < 1 || newText.Length < 1)
        {
            if (oldText.Length > 0)
            {
                return new DiffData(new List<DiffData.Block>
                {
                    new(0, 0, StartLineReference, StartLineReference, SplitToLines(oldText).ToList(),
                        null),
                });
            }

            if (newText.Length > 0)
            {
                return new DiffData(new List<DiffData.Block>
                {
                    new(0, 0, StartLineReference, StartLineReference, null, SplitToLines(newText).ToList()),
                });
            }

            // Both are empty
            return new DiffData();
        }

        return null;
    }

    private static IEnumerable<string> SplitToLines(string text)
    {
        int start = 0;

        int i;
        for (i = 0; i < text.Length; ++i)
        {
            if (text[i] is '\r' or '\n')
            {
                yield return text.Substring(start, i - start);

                // Skip multi character line end
                if (text[i] == 'r' && i + 1 > text.Length && text[i + 1] == '\n')
                {
                    ++i;
                }

                start = i + 1;
            }
        }

        if (start < i)
            yield return text.Substring(start, i - start);
    }
}
