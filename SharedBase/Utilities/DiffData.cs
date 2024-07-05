namespace SharedBase.Utilities;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

/// <summary>
///   Data from a diff operation. This data format should not be changed as this is stored in the database as that
///   would break a ton of history.
/// </summary>
public class DiffData
{
    [JsonInclude]
    [JsonPropertyName("blocks")]
    public readonly List<Block>? Blocks;

    [JsonInclude]
    [JsonPropertyName("winStyle")]
    public bool PreferWindowsLineEndings;

    /// <summary>
    ///   Blank diff with no changes
    /// </summary>
    public DiffData()
    {
    }

    [JsonConstructor]
    public DiffData(List<Block>? blocks)
    {
        if (blocks is { Count: > 0 })
            Blocks = blocks;
    }

    /// <summary>
    ///   True when the diffed data is the same, i.e. there are no differences
    /// </summary>
    [JsonIgnore]
    public bool Empty => Blocks == null || Blocks.Count < 1;

    /// <summary>
    ///   A part of the diff, a single block of lines where there are changes
    /// </summary>
    public struct Block
    {
        /// <summary>
        ///   Offset expected after the previous block where this block is found. This is not exact to allow multiple
        ///   diffs generated from the same original version to be applied in sequence as long as they modify different
        ///   parts. This is in units of lines after the start of the previous block.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///     So a block at the start of the text will have offset 0. And if there's a one line block, one matching
        ///     line, and then another block this will be 2. 0 is not a valid value as two blocks shouldn't be able to
        ///     start on the same line (as they should be combined).
        ///   </para>
        /// </remarks>
        [JsonInclude]
        [JsonPropertyName("offset")]
        public readonly int ExpectedOffset;

        /// <summary>
        ///   If there are lines matching <see cref="Reference1"/> that aren't part of this block, this is over 0 and
        ///   that many instances of that reference line should be skipped before looking for the real line.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("refSkip")]
        public readonly int IgnoreReferenceCount;

        /// <summary>
        ///   Reference lines are used to detect where this block of operations should be performed.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("ref1")]
        public readonly string Reference1;

        [JsonInclude]
        [JsonPropertyName("ref2")]
        public readonly string Reference2;

        [JsonInclude]
        [JsonPropertyName("delete")]
        public List<string>? DeletedLines;

        [JsonInclude]
        [JsonPropertyName("add")]
        public List<string>? AddedLines;

        [JsonConstructor]
        public Block(int expectedOffset, int ignoreReferenceCount, string reference1, string reference2,
            List<string>? deletedLines, List<string>? addedLines)
        {
            ExpectedOffset = expectedOffset;
            IgnoreReferenceCount = ignoreReferenceCount;
            Reference1 = reference1;
            Reference2 = reference2;

            if (deletedLines is { Count: > 0 })
                DeletedLines = deletedLines;

            if (addedLines is { Count: > 0 })
                AddedLines = addedLines;
        }

        public static bool operator ==(Block left, Block right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Block left, Block right)
        {
            return !left.Equals(right);
        }

        public bool Equals(Block other)
        {
            if (ReferenceEquals(DeletedLines, null) && !ReferenceEquals(other.DeletedLines, null))
                return false;
            if (ReferenceEquals(AddedLines, null) && !ReferenceEquals(other.AddedLines, null))
                return false;

            if (DeletedLines != null)
            {
                if (other.DeletedLines == null || !DeletedLines.SequenceEqual(other.DeletedLines))
                    return false;
            }

            if (AddedLines != null)
            {
                if (other.AddedLines == null || !AddedLines.SequenceEqual(other.AddedLines))
                    return false;
            }

            return ExpectedOffset == other.ExpectedOffset && IgnoreReferenceCount == other.IgnoreReferenceCount &&
                Reference1 == other.Reference1 && Reference2 == other.Reference2;
        }

        public override bool Equals(object? obj)
        {
            return obj is Block other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ExpectedOffset, IgnoreReferenceCount, Reference1, Reference2, DeletedLines,
                AddedLines);
        }
    }
}
