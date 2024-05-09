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

        var reader1 = new LineByLineReader(oldText);
        var reader2 = new LineByLineReader(newText);

        var resultBlocks = new List<DiffData.Block>();

        bool openBlock = false;
        var blockData = default(DiffData.Block);

        // Compare the strings line by line
        while (true)
        {
            // See if we can process a full line
            bool lineEnded1 = reader1.LookForLineEnd();
            bool lineEnded2 = reader2.LookForLineEnd();

            if (reader1.Ended || reader2.Ended)
                break;

            if (reader1.CompareCurrentLineWith(reader2))
            {
                // Lines match
            }
            else
            {
                // Divergence

                // Try to find where the lines would converge if possible, if not possible then this just records
                // differences until the end

                var readerSearch2 = reader2.Clone();

                if (lineEnded2)
                    readerSearch2.MoveToNextLine();

                bool foundReConvergence = false;

                while (!readerSearch2.Ended)
                {
                    bool foundMore = readerSearch2.LookForLineEnd();

                    if (!foundMore && readerSearch2.Ended)
                        break;

                    if (reader1.CompareCurrentLineWith(readerSearch2))
                    {
                        foundReConvergence = true;

                        if (readerSearch2.LookBackwardsForLineEnd())
                            readerSearch2.MoveToPreviousLine();
                        break;
                    }

                    if (foundMore)
                        readerSearch2.MoveToNextLine();
                }

                if (!foundReConvergence)
                {
                    // Cannot re-converge, add the divergence

                    if (!openBlock)
                    {
                        OnLineDifference(ref reader1, ref reader2, out blockData, resultBlocks);
                        openBlock = true;
                    }

                    // Add divergence
                    blockData.AddedLines ??= [];
                    blockData.AddedLines.Add(reader2.ReadCurrentLineToStart());

                    blockData.DeletedLines ??= [];
                    blockData.DeletedLines.Add(reader1.ReadCurrentLineToStart());
                }
                else
                {
                    // Record changes from reader 2 until it finds the search point for re-convergence
                    if (!openBlock)
                    {
                        OnLineDifference(ref reader1, ref reader2, out blockData, resultBlocks);
                    }

                    blockData.AddedLines ??= [];

                    while (reader2.IsBehind(readerSearch2))
                    {
                        blockData.AddedLines.Add(reader2.ReadCurrentLineToStart());

                        if (reader2.AtLineEnd)
                            reader2.MoveToNextLine();

                        lineEnded2 = reader2.LookForLineEnd();
                    }

                    // No longer diverging
                    resultBlocks.Add(blockData);
                    openBlock = false;
                }
            }

            if (lineEnded1)
                reader1.MoveToNextLine();

            if (lineEnded2)
                reader2.MoveToNextLine();
        }

        // More old lines than new
        while (!reader1.Ended)
        {
            if (!openBlock)
            {
                OnLineDifference(ref reader1, ref reader2, out blockData, resultBlocks);
                openBlock = true;
            }

            blockData.DeletedLines ??= [];
            blockData.DeletedLines.Add(reader1.ReadCurrentLineToStart());

            bool lineEnded = reader1.LookForLineEnd();

            if (reader1.Ended)
                break;

            if (lineEnded)
                reader1.MoveToNextLine();
        }

        // More new lines than old
        while (!reader2.Ended)
        {
            if (!openBlock)
            {
                OnLineDifference(ref reader1, ref reader2, out blockData, resultBlocks);
                openBlock = true;
            }

            blockData.AddedLines ??= [];
            blockData.AddedLines.Add(reader2.ReadCurrentLineToStart());

            bool lineEnded = reader2.LookForLineEnd();

            if (reader2.Ended)
                break;

            if (lineEnded)
                reader2.MoveToNextLine();
        }

        // End the final block
        if (openBlock)
            resultBlocks.Add(blockData);

        /*
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
        }*/

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
    ///   Starts a new block when line differences are found
    /// </summary>
    private static void OnLineDifference(ref LineByLineReader oldReader, ref LineByLineReader newReader,
        out DiffData.Block block, List<DiffData.Block> previousBlocks)
    {
        bool hasReferenceBlock = false;
        int absoluteOffsetOfPrevious = 0;

        if (previousBlocks.Count > 0)
        {
            hasReferenceBlock = true;
            absoluteOffsetOfPrevious = previousBlocks.Sum(b => b.ExpectedOffset);
        }

        var referenceLineScanner = oldReader.Clone();

        // If the reader is already at the end, then need to consider even the last line for reference purposes
        if (!referenceLineScanner.Ended)
        {
            // Move into the current line properly to be able to find a valid previous line
            if (referenceLineScanner.AtLineEnd)
                referenceLineScanner.MoveToPreviousLine();

            if (!referenceLineScanner.LookBackwardsForLineEnd())
            {
                // At the start of the text
                block = new DiffData.Block(0, 0, StartLineReference, StartLineReference, null, null);
                return;
            }

            referenceLineScanner.MoveToPreviousLine();
        }
        else
        {
            referenceLineScanner.MoveBackwardsFromEnd();
        }

        // This variable calculates approximate distance to the previous block
        int linesFromPreviousBlock = 1;

        // Look for reference lines to use to mark the start of this block
        string? reference1 = null;
        string? reference2 = null;

        // If the reference line exists in the source between the previous block and start of this one, those
        // need to be ignored to not place new content at incorrect places
        int skipReferenceLines = 0;

        while (true)
        {
            var line = referenceLineScanner.ReadCurrentLineToStart();

            // If the line happens to equal the start line reference, escape it
            if (line == StartLineReference)
                line = "\\" + StartLineReference;

            // Assign the line references as we read back in the right order
            if (reference2 == null)
            {
                reference2 = line;
            }
            else if (reference1 == null)
            {
                reference1 = line;
            }
            else
            {
                if (reference1 == line)
                {
                    ++skipReferenceLines;
                }
            }

            // Detect if we have found the tail of the previous block
            if (hasReferenceBlock && referenceLineScanner.LineIndex == absoluteOffsetOfPrevious)
            {
                break;
            }

            if (referenceLineScanner.LookBackwardsForLineEnd())
            {
                // Still more lines exist
                referenceLineScanner.MoveToPreviousLine();
                ++linesFromPreviousBlock;
            }
            else
            {
                // Reached start of the text

                // Set references that are unset to match the start
                reference1 ??= StartLineReference;
                reference2 ??= StartLineReference;

                break;
            }
        }

        if (reference1 == null || reference2 == null)
            throw new Exception("Diff logic fail, couldn't find both reference lines");

        block = new DiffData.Block(linesFromPreviousBlock, skipReferenceLines, reference1, reference2, null, null);
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
                    new(0, 0, StartLineReference, StartLineReference,
                        LineByLineReader.SplitToLines(oldText).ToList(),
                        null),
                });
            }

            if (newText.Length > 0)
            {
                return new DiffData(new List<DiffData.Block>
                {
                    new(0, 0, StartLineReference, StartLineReference, null,
                        LineByLineReader.SplitToLines(newText).ToList()),
                });
            }

            // Both are empty
            return new DiffData();
        }

        return null;
    }
}
