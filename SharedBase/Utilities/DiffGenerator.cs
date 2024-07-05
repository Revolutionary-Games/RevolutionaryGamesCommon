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

    /// <summary>
    ///   How many lines slight deviance mode looks at from the start of the text when resolving a block that targets
    ///   the beginning of the text.
    /// </summary>
    private const int SlightDevianceBeginningAllowedLines = 10;

    /// <summary>
    ///   How many deleted lines empty source text is allowed to have before added lines are not allowed to be added
    /// </summary>
    private const int SlightDevianceEmptyDeletedLines = 5;

    /// <summary>
    ///   How closely diff blocks must match their reference lines to be able to apply
    /// </summary>
    public enum DiffMatchMode
    {
        /// <summary>
        ///   Reference lines must be exactly where expected, otherwise diff applying fails.
        ///   WARNING: not tested very well (especially reference line counting might not be exact causing unnecessary
        ///   failures)
        /// </summary>
        Strict,

        /// <summary>
        ///   Allows slight deviance in reference lines where the read position is adjusted on the fly a bit to look
        ///   for matching lines that aren't exactly at the right location
        /// </summary>
        NormalSlightDeviance,
    }

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

        bool lineEnded1 = false;
        bool lineEnded2 = false;

        // Compare the strings line by line
        while (true)
        {
            // See if we can process a full line
            bool newEnd1 = reader1.LookForLineEnd();
            bool newEnd2 = reader2.LookForLineEnd();

            if (!reader1.Ended && !reader2.Ended)
            {
                lineEnded1 = newEnd1;
                lineEnded2 = newEnd2;
            }
            else
            {
                // Something ended, can no longer process lines in sequence

                if (!reader1.Ended)
                    lineEnded1 = newEnd1;

                if (!reader2.Ended)
                    lineEnded2 = newEnd2;

                break;
            }

            if (reader1.CompareCurrentLineWith(reader2))
            {
                // Lines match
                if (openBlock)
                {
                    // Difference block ended due to matching lines
                    resultBlocks.Add(blockData);
                    openBlock = false;
                }
            }
            else
            {
                // Divergence

                // Try to find where the lines would converge if possible, if not possible then this just records
                // differences until the end
                var readerSearch2 = reader2.Clone();

                bool foundReConvergence = false;
                bool skip = false;

                // Only allow normal re-convergence after the first difference line is processed
                if (openBlock)
                {
                    if (lineEnded2)
                        readerSearch2.MoveToNextLine();

                    while (!readerSearch2.Ended)
                    {
                        bool foundMore = readerSearch2.LookForLineEnd();

                        // TODO: should this allow comparing even incomplete lines here?
                        if (!foundMore || readerSearch2.Ended)
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
                }
                else
                {
                    // Except in the case that the next line matches the one in new text, this signals a single line
                    // removed in the old text
                    var readerSearch1 = reader1.Clone();

                    // TODO: should this move to next be removed and then instead just always skip if there isn't a
                    // next line?
                    if (lineEnded1)
                        readerSearch1.MoveToNextLine();

                    readerSearch1.LookForLineEnd();

                    if (!readerSearch1.Ended && readerSearch1.CompareCurrentLineWith(reader2))
                    {
                        if (openBlock)
                            throw new Exception("A block shouldn't be open here");

                        // Just a single deleted line
                        OnLineDifference(ref reader1, ref reader2, out blockData, resultBlocks);

                        blockData.DeletedLines = [reader1.ReadCurrentLineToStart()];
                        resultBlocks.Add(blockData);
                        openBlock = false;

                        // Move reader2 back one line so that the lines match up on the next iteration
                        if (lineEnded2)
                            reader2.MoveToPreviousLine();

                        lineEnded2 = reader2.LookBackwardsForLineEnd();

                        skip = true;
                    }
                }

                if (!skip)
                {
                    if (!openBlock)
                    {
                        OnLineDifference(ref reader1, ref reader2, out blockData, resultBlocks);
                        openBlock = true;
                    }

                    if (!foundReConvergence)
                    {
                        // Cannot re-converge, add the divergence

                        // Add divergence
                        blockData.AddedLines ??= [];
                        blockData.AddedLines.Add(reader2.ReadCurrentLineToStart());

                        blockData.DeletedLines ??= [];
                        blockData.DeletedLines.Add(reader1.ReadCurrentLineToStart());
                    }
                    else
                    {
                        // Record changes from reader 2 until it finds the search point for re-convergence
                        blockData.AddedLines ??= [];

                        while (reader2.IsBehind(readerSearch2))
                        {
                            blockData.AddedLines.Add(reader2.ReadCurrentLineToStart());

                            if (reader2.AtLineEnd)
                                reader2.MoveToNextLine();

                            bool ended = reader2.LookForLineEnd();

                            if (!reader2.Ended)
                                lineEnded2 = ended;
                        }

                        // No longer diverging
                        resultBlocks.Add(blockData);
                        openBlock = false;
                    }
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

            if (lineEnded1)
                reader1.MoveToNextLine();

            bool lineEnded = reader1.LookForLineEnd();

            if (reader1.Ended)
            {
                // If the new text did not end with a newline, add a blank deleted line to make applying set the
                // correct trailing newline
                if (!lineEnded2)
                    blockData.DeletedLines.Add(string.Empty);

                break;
            }

            lineEnded1 = lineEnded;

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

            if (lineEnded2)
                reader2.MoveToNextLine();

            bool lineEnded = reader2.LookForLineEnd();

            if (reader2.Ended)
            {
                // TODO: does this need similar handling as the case above that adds removal of trailing newline?
                break;
            }

            lineEnded2 = lineEnded;

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

        return new DiffData(resultBlocks)
        {
            PreferWindowsLineEndings = ResultShouldUseWindowsLineEndings(oldText, newText),
        };
    }

    /// <summary>
    ///   Applies a diff to a piece of text
    /// </summary>
    /// <param name="original">Text to apply the diff to</param>
    /// <param name="diff">Diff data to apply, if empty won't do anything to the text</param>
    /// <param name="matchMode">How closely the diff must match the text to allow it to apply</param>
    /// <param name="reuseBuilder">If provided this string builder is used for the text</param>
    /// <returns>A string builder containing the result</returns>
    /// <exception cref="ArgumentException">If the diff data is malformed</exception>
    /// <exception cref="NonMatchingDiffException">
    ///   Thrown when the diff cannot be applied as the <see cref="original"/> text is too different
    /// </exception>
    public StringBuilder ApplyDiff(string original, DiffData diff,
        DiffMatchMode matchMode = DiffMatchMode.NormalSlightDeviance, StringBuilder? reuseBuilder = null)
    {
        if (reuseBuilder == null)
        {
            reuseBuilder = new StringBuilder(original.Length);
        }
        else
        {
            reuseBuilder.Clear();
        }

        // Just return original if nothing in diff
        if (diff.Blocks == null || diff.Blocks.Count < 1)
        {
            return reuseBuilder.Append(original);
        }

        // Special case where original is empty, just apply line adds
        if (string.IsNullOrEmpty(original))
        {
            HandleBlocksToEmptySourceWrite(reuseBuilder, diff, matchMode);
            return reuseBuilder;
        }

        // Detect line ending type to use
        var lineEndings = "\n";

        // Probably good enough heuristic to switch to Windows style if there is at least one such line ending
        // TODO: how should the situation be handled where windows style line endings want to be completely removed?
        // Switch on the line endings mode from the diff data if during generation it was determined that windows style
        // line endings should be used
        if (original.Contains("\r\n") || diff.PreferWindowsLineEndings)
        {
            lineEndings = "\r\n";
        }

        bool seenBeginningBlock = false;

        // Blocks count their estimated positions from the start of the previous block, not its end so this is used
        // to adjust things to match up
        // TODO: add a proper test for strict mode diff
        int blockLineAdjustment = 0;

        var originalReader = new LineByLineReader(original);

        foreach (var block in diff.Blocks)
        {
            if (block.Reference1 == null! || block.Reference2 == null!)
                throw new ArgumentException("Diff block has either reference 1 or 2 set as null, this is never valid");

            int referenceIgnores = block.IgnoreReferenceCount;

            // Block goes to the start of the text
            if (block.Reference1 == StartLineReference || block.Reference2 == StartLineReference)
            {
                if (block.Reference1 != StartLineReference)
                {
                    throw new ArgumentException(
                        "Diff block earlier reference should refer to the text beginning for a " +
                        "block that is at the start");
                }

                if (referenceIgnores > 0)
                    throw new ArgumentException("In a beginning diff block reference ignores should be 0");

                // TODO: should multiple blocks targeting the beginning be allowed?
                // This would be pretty difficult in needing to modify lines already processed to the string builder
                if (seenBeginningBlock)
                {
                    throw new ArgumentException(
                        "There cannot be multiple diff blocks for the beginning (or beginning blocks " +
                        "after other blocks)");
                }

                // Re-initialize the reader to go back to the start
                originalReader = new LineByLineReader(original);
                seenBeginningBlock = true;

                if (block.Reference2 != StartLineReference)
                {
                    // Reference line must match for the block to match

                    int linesLeft = SlightDevianceBeginningAllowedLines;

                    while (true)
                    {
                        bool lineEnd = originalReader.LookForLineEnd();

                        if (originalReader.Ended)
                            throw new NonMatchingDiffException(block);

                        var line = originalReader.ReadCurrentLineToStart();

                        if (lineEnd)
                            originalReader.MoveToNextLine();

                        // Copy viewed reference lines to output
                        CopyLineToOutput(reuseBuilder, line, lineEnd, lineEndings);

                        --linesLeft;

                        if (line == block.Reference2)
                        {
                            // Found correct position
                            break;
                        }

                        if (matchMode == DiffMatchMode.Strict || linesLeft <= 0)
                        {
                            throw new NonMatchingDiffException(block);
                        }
                    }
                }

                blockLineAdjustment = ApplyBlock(reuseBuilder, ref originalReader, block, matchMode, lineEndings);
                continue;
            }

            // Normal block (not at start)
            // Disallow beginning blocks to come later
            seenBeginningBlock = true;

            // Look for the reference lines
            int lineEstimate = block.ExpectedOffset - blockLineAdjustment;

            // Use a separate scanning reader to be able to do fuzzy matching if exact fails
            // TODO: implement fuzzy matching modes (first should be whitespace ignoring mode)
            var scanReader = originalReader.Clone();

            bool reference1Matched = false;

            while (true)
            {
                bool lineEnd = scanReader.LookForLineEnd();

                if (scanReader.Ended)
                    throw new NonMatchingDiffException(block);

                var line = scanReader.ReadCurrentLineToStart();

                if (lineEnd)
                    scanReader.MoveToNextLine();

                --lineEstimate;

                if (reference1Matched)
                {
                    if (line == block.Reference2)
                    {
                        // Found correct position

                        // -1 is checked here as reference1 is where the lines point to so if both 1 and 2 references
                        // match we are already one line past where we wanted to be
                        if (matchMode == DiffMatchMode.Strict && lineEstimate != 0 && lineEstimate != -1)
                        {
                            // Fail in strict mode if line wasn't exactly where we expected (too soon)
                            throw new NonMatchingDiffException(block);
                        }

                        break;
                    }

                    // Not a match as second line didn't match
                    reference1Matched = false;
                }
                else
                {
                    if (line == block.Reference1 && --referenceIgnores < 0)
                    {
                        // Potential place where the references point to
                        reference1Matched = true;
                        continue;
                    }
                }

                if (matchMode == DiffMatchMode.Strict && lineEstimate < 0)
                {
                    throw new NonMatchingDiffException(block);
                }
            }

            // Copy viewed reference lines to output
            while (originalReader.IsBehind(scanReader))
            {
                bool lineEnd = originalReader.LookForLineEnd();

                if (originalReader.Ended)
                    throw new NonMatchingDiffException(block);

                var line = originalReader.ReadCurrentLineToStart();

                if (lineEnd)
                    originalReader.MoveToNextLine();

                MakeSureResultHasEndingNewLine(reuseBuilder, lineEndings);
                CopyLineToOutput(reuseBuilder, line, lineEnd, lineEndings);
            }

            // Then apply the block as the reader should be at the start of the first line of the block
            blockLineAdjustment = ApplyBlock(reuseBuilder, ref originalReader, block, matchMode, lineEndings);
        }

        // After blocks have ended copy all leftover lines
        CopyRemainingTextToOutput(reuseBuilder, originalReader, lineEndings);

        return reuseBuilder;
    }

    private static void MakeSureResultHasEndingNewLine(StringBuilder reuseBuilder, string lineEndings)
    {
        if (reuseBuilder.Length > 0 && reuseBuilder[^1] != '\n')
            reuseBuilder.Append(lineEndings);
    }

    /// <summary>
    ///   Applies a single block in a diff (the <see cref="originalReader"/> must be at the line after the position
    ///   the block reference mandates)
    /// </summary>
    /// <returns>The number of lines processed from <see cref="originalReader"/> to handle the block</returns>
    private static int ApplyBlock(StringBuilder reuseBuilder, ref LineByLineReader originalReader,
        in DiffData.Block block, DiffMatchMode matchMode, string lineEndings)
    {
        // Need to make sure there's a newline before applying the block data
        MakeSureResultHasEndingNewLine(reuseBuilder, lineEndings);

        int readLines = 0;

        // Handle deleted lines
        if (block.DeletedLines is { Count: > 0 })
        {
            bool lastLineProcessed = false;

            foreach (var deletedLine in block.DeletedLines)
            {
                bool lineEnd = originalReader.LookForLineEnd();

                if (originalReader.Ended)
                {
                    // Deleting trailing newline potentially, or mismatching data
                    if (deletedLine == string.Empty && !lastLineProcessed)
                    {
                        lastLineProcessed = true;

                        if (reuseBuilder.Length > 0 && reuseBuilder[^1] == '\n')
                        {
                            reuseBuilder.Remove(reuseBuilder.Length - 1, 1);
                        }

                        continue;
                    }

                    throw new NonMatchingDiffException(block);
                }

                ++readLines;

                var line = originalReader.ReadCurrentLineToStart();

                if (lineEnd)
                    originalReader.MoveToNextLine();

                if (line != deletedLine)
                {
                    if (matchMode != DiffMatchMode.Strict)
                    {
                        // Allow differing by whitespace (and case)
                    }

                    // TODO: maybe allow skipping applying and skipping forward looking for the reference lines again
                    // if the lines to delete cannot be found at this location (when not in strict mode)
                    throw new NonMatchingDiffException(block);
                }

                // Removed line, don't write to output, and then it is just now handled by doing nothing
            }
        }

        if (block.AddedLines is { Count: > 0 })
        {
            // Only apply newlines between added lines as an added line may not necessarily end with a newline (for
            // example at the end of a file)
            bool first = true;

            foreach (var addedLine in block.AddedLines)
            {
                if (!first)
                    reuseBuilder.Append(lineEndings);

                reuseBuilder.Append(addedLine);

                first = false;
            }
        }

        // Empty blocks shouldn't be dangerous so this doesn't check if this did anything or not

        return readLines;
    }

    private static void HandleBlocksToEmptySourceWrite(StringBuilder reuseBuilder, DiffData data,
        DiffMatchMode matchMode)
    {
        if (data.Blocks == null)
            return;

        // TODO: should this use different line endings in some mode?
        string lineEndings = "\n";
        bool first = true;

        int deletedLinesLeft = SlightDevianceEmptyDeletedLines;

        foreach (var block in data.Blocks)
        {
            if (block.AddedLines != null)
            {
                foreach (var line in block.AddedLines)
                {
                    if (!first)
                        reuseBuilder.Append(lineEndings);

                    reuseBuilder.Append(line);
                    first = false;
                }
            }

            if (block.DeletedLines is { Count: > 0 })
            {
                deletedLinesLeft -= block.DeletedLines.Count;

                if (matchMode == DiffMatchMode.Strict || deletedLinesLeft < 0)
                {
                    throw new NonMatchingDiffException(block);
                }
            }
        }
    }

    private static void CopyLineToOutput(StringBuilder reuseBuilder, string line, bool lineEnd,
        string lineEndings)
    {
        reuseBuilder.Append(line);

        if (lineEnd)
            reuseBuilder.Append(lineEndings);
    }

    private static void CopyRemainingTextToOutput(StringBuilder reuseBuilder, LineByLineReader originalReader,
        string lineEndings)
    {
        bool first = true;

        while (!originalReader.Ended)
        {
            if (originalReader.AtLineEnd)
                originalReader.MoveToNextLine();

            bool lineEnd = originalReader.LookForLineEnd();

            if (originalReader.Ended)
                break;

            // If this was previously processing data that didn't end up with a newline should put one here
            if (first)
                MakeSureResultHasEndingNewLine(reuseBuilder, lineEndings);

            reuseBuilder.Append(originalReader.ReadCurrentLineToStart());

            if (lineEnd)
                reuseBuilder.Append(lineEndings);

            first = false;
        }
    }

    /// <summary>
    ///   Starts a new block when line differences are found
    /// </summary>
    private static void OnLineDifference(ref LineByLineReader oldReader, ref LineByLineReader newReader,
        out DiffData.Block block, List<DiffData.Block> previousBlocks)
    {
        // TODO: double check that this parameter wasn't meant to be used
        _ = newReader;

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

                // According to the compiler this can never be null here
                // reference2 ??= StartLineReference;

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
                })
                {
                    PreferWindowsLineEndings = ResultShouldUseWindowsLineEndings(oldText, newText),
                };
            }

            if (newText.Length > 0)
            {
                return new DiffData(new List<DiffData.Block>
                {
                    new(0, 0, StartLineReference, StartLineReference, null,
                        LineByLineReader.SplitToLines(newText).ToList()),
                })
                {
                    PreferWindowsLineEndings = ResultShouldUseWindowsLineEndings(oldText, newText),
                };
            }

            // Both are empty
            return new DiffData();
        }

        return null;
    }

    private static bool ResultShouldUseWindowsLineEndings(string oldText, string newText)
    {
        return oldText.Contains("\r\n") || newText.Contains("\r\n");
    }
}
