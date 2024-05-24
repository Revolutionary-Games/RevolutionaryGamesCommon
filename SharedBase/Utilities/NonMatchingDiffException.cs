namespace SharedBase.Utilities;

using System;

/// <summary>
///   Thrown when a diff cannot be applied because the original text is too different and diff block references are not
///   matching
/// </summary>
public class NonMatchingDiffException : Exception
{
    public NonMatchingDiffException(DiffData.Block block) : base("Non-matching diff block found, cannot apply diff")
    {
        Block = block;
    }

    public DiffData.Block Block { get; }
}
