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
        DiffData.Block blockData = default(DiffData.Block);

        bool convergencePossible = true;

        // Compare the strings line by line
        while (!reader1.Ended && !reader2.Ended)
        {
            // See if we can process a full line
            bool ended1 = reader1.LookForLineEnd();
            bool ended2 = reader2.LookForLineEnd();

            /*if (!convergencePossible)
            {
                // Record differences until the end
                if (!openBlock)
                    throw new Exception("Block should be open");
            }
            else*/
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
                        OnLineDifference(ref reader1, ref reader2, ref blockData, resultBlocks);
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
                        OnLineDifference(ref reader1, ref reader2, ref blockData, resultBlocks);
                    }

                    blockData.AddedLines ??= [];

                    while (reader2.IsBehind(readerSearch2))
                    {
                        blockData.AddedLines.Add(reader2.ReadCurrentLineToStart());

                        if (reader2.AtLineEnd)
                            reader2.MoveToNextLine();

                        ended2 = reader2.LookForLineEnd();
                    }

                    // No longer diverging
                    resultBlocks.Add(blockData);
                    openBlock = false;
                }

                if (ended1)
                    reader1.MoveToNextLine();

                if (ended2)
                    reader2.MoveToNextLine();
            }
        }

        // More old lines than new
        while (!reader1.Ended)
        {
            bool ended = reader1.LookForLineEnd();

            var line = reader1.ReadCurrentLineToStart();

            throw new NotImplementedException();

            if (ended)
                reader1.MoveToNextLine();
        }

        // More new lines than old
        while (!reader2.Ended)
        {
            bool ended = reader2.LookForLineEnd();

            var line = reader2.ReadCurrentLineToStart();

            throw new NotImplementedException();

            if (ended)
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
        ref DiffData.Block block, List<DiffData.Block> previousBlocks)
    {
        // TODO: scan backwards for reference lines
        // throw new NotImplementedException();

        // StartLineReference

        // TODO: detect how many instances of text to skip

        // TODO: calculate the approximate distance to previous block
        // block.ExpectedOffset

        block.DeletedLines?.Clear();
        block.AddedLines?.Clear();

        /*// Old text is removed and new line is added at the block
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
        }*/
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
