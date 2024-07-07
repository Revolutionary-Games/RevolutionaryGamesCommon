namespace SharedBase.Tests.Utilities.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SharedBase.Utilities;
using Xunit;

public class DiffGeneratorTests
{
    private const string Text1 = """
                                 This is just some text
                                 with a few lines in it
                                 that just says basically nothing at all
                                 """;

    private const string Text2 = """
                                 This is just some text
                                 with a few lines in it
                                 that just says basically nothing at all
                                 but maybe just has a bit of a new thing
                                 """;

    private const string Text3 = """
                                 A simple piece of text
                                 """;

    private const string Text4 = """
                                 But what if
                                 """;

    private const string Text5 = "Just some text";
    private const string Text6 = Text5 + "\n";

    private const string Text7 = """
                                 First line difference in text
                                 with a few lines in it
                                 that just says basically nothing at all
                                 """;

    private const string Text8 = """
                                 This is just some text
                                 with a few lines in it
                                 and there ends up being a difference
                                 that just says basically nothing at all
                                 but maybe just has a bit of a new thing
                                 and there ends up being a difference
                                 that is after a reference match
                                 but only after a changed line
                                 and ends with a match
                                 """;

    private const string Text9 = """
                                 This is just some text
                                 with a few lines in it
                                 and there ends up being a difference
                                 that just says basically nothing at all
                                 but maybe just has a bit of a new thing
                                 and there ends up being a difference
                                 that is after a reference match
                                 that might be tricky to match
                                 and ends with a match
                                 """;

    private const string Text10 = """
                                  This is just some text
                                  with a few changed lines in it
                                  and there ends up being a difference
                                  that just says basically nothing at all
                                  but maybe just has a bit of a new thing
                                  and there ends up being a difference in multiple places
                                  that is after a reference match
                                  that might be tricky to match
                                  and ends with a match
                                  """;

    private const string Text11 = """
                                  This is just some text
                                  that just says basically nothing at all
                                  """;

    private const string SpecificText1Old = """
                                            This is just some text
                                            with multiple lines
                                            that may be changed in the future
                                            to be something else
                                            """;

    private const string SpecificText1New = """
                                            This is just some text
                                            with multitude of lines
                                            that may be changed in the future
                                            and lines inserted
                                            as well as deleted
                                            """;

    [Fact]
    public void Diff_EmptyIsEmpty()
    {
        var diff = DiffGenerator.Default.Generate(Text1, Text1);

        Assert.True(diff.Empty);
        Assert.Null(diff.Blocks);
    }

    [Fact]
    public void Diff_WithJustNewText()
    {
        var diff = DiffGenerator.Default.Generate(string.Empty, Text1);

        Assert.False(diff.Empty);
        Assert.NotNull(diff.Blocks);

        Assert.Single(diff.Blocks);

        var block = diff.Blocks.First();

        Assert.Null(block.DeletedLines);
        Assert.NotNull(block.AddedLines);

        Assert.Equal(Text1.Split('\n', '\r', StringSplitOptions.RemoveEmptyEntries), block.AddedLines);

        Assert.Equal(0, block.ExpectedOffset);
        Assert.Equal(0, block.IgnoreReferenceCount);
    }

    [Fact]
    public void Diff_WithJustOldText()
    {
        var diff = DiffGenerator.Default.Generate(Text1, string.Empty);

        Assert.False(diff.Empty);
        Assert.NotNull(diff.Blocks);

        Assert.Single(diff.Blocks);

        var block = diff.Blocks.First();

        Assert.NotNull(block.DeletedLines);
        Assert.Null(block.AddedLines);

        Assert.Equal(Text1.Split('\n', '\r', StringSplitOptions.RemoveEmptyEntries), block.DeletedLines);

        Assert.Equal(0, block.ExpectedOffset);
        Assert.Equal(0, block.IgnoreReferenceCount);
    }

    [Fact]
    public void Diff_OneLineIsReplaced()
    {
        var diff = DiffGenerator.Default.Generate(Text3, Text4);

        Assert.False(diff.Empty);
        Assert.NotNull(diff.Blocks);

        Assert.Single(diff.Blocks);

        var block = diff.Blocks.First();

        Assert.NotNull(block.DeletedLines);
        Assert.NotNull(block.AddedLines);
        Assert.Single(block.AddedLines);
        Assert.Single(block.DeletedLines);
        Assert.Equal(Text3, block.DeletedLines[0]);
        Assert.Equal(Text4, block.AddedLines[0]);
        Assert.Equal(0, block.ExpectedOffset);
        Assert.Equal(0, block.IgnoreReferenceCount);
        Assert.NotEqual(Text3, block.Reference1);
        Assert.NotEqual(Text3, block.Reference2);
        Assert.NotEqual(Text4, block.Reference1);
        Assert.NotEqual(Text4, block.Reference2);
    }

    [Fact]
    public void Diff_SimpleAppendIsGenerated()
    {
        var diff = DiffGenerator.Default.Generate(Text1, Text2);

        Assert.False(diff.Empty);
        Assert.NotNull(diff.Blocks);

        Assert.Single(diff.Blocks);

        var block = diff.Blocks.First();

        Assert.Null(block.DeletedLines);
        Assert.NotNull(block.AddedLines);
        Assert.Single(block.AddedLines);
        Assert.Equal("but maybe just has a bit of a new thing", block.AddedLines[0]);
        Assert.Equal(3, block.ExpectedOffset);
        Assert.Equal(0, block.IgnoreReferenceCount);
    }

    [Fact]
    public void Diff_WithJustNewLineDifference()
    {
        var diff = DiffGenerator.Default.Generate(Text5, Text6);

        Assert.NotNull(diff.Blocks);
        Assert.Single(diff.Blocks);

        var block = diff.Blocks.First();

        Assert.Null(block.DeletedLines);
        Assert.NotNull(block.AddedLines);
        Assert.Single(block.AddedLines);
        Assert.Equal(string.Empty, block.AddedLines[0]);

        Assert.Equal(DiffGenerator.StartLineReference, block.Reference1);
        Assert.Equal("Just some text", block.Reference2);

        Assert.Equal(1, block.ExpectedOffset);
        Assert.Equal(0, block.IgnoreReferenceCount);
    }

    [Fact]
    public void Diff_FirstLineDifference()
    {
        var diff = DiffGenerator.Default.Generate(Text1, Text7);

        Assert.NotNull(diff.Blocks);
        Assert.Single(diff.Blocks);

        var block = diff.Blocks.First();

        Assert.NotNull(block.DeletedLines);
        Assert.Single(block.DeletedLines);
        Assert.Equal("This is just some text", block.DeletedLines[0]);
        Assert.NotNull(block.AddedLines);
        Assert.Single(block.AddedLines);
        Assert.Equal("First line difference in text", block.AddedLines[0]);

        Assert.Equal(DiffGenerator.StartLineReference, block.Reference1);
        Assert.Equal(DiffGenerator.StartLineReference, block.Reference2);

        Assert.Equal(0, block.ExpectedOffset);
        Assert.Equal(0, block.IgnoreReferenceCount);
    }

    [Fact]
    public void Diff_OneDeletedLine()
    {
        var diff = DiffGenerator.Default.Generate(Text1, Text11);

        Assert.NotNull(diff.Blocks);
        Assert.Single(diff.Blocks);

        var block = diff.Blocks.First();

        Assert.NotNull(block.DeletedLines);
        Assert.Single(block.DeletedLines);
        Assert.Equal("with a few lines in it", block.DeletedLines[0]);
        Assert.Null(block.AddedLines);

        Assert.Equal(DiffGenerator.StartLineReference, block.Reference1);
        Assert.Equal("This is just some text", block.Reference2);

        Assert.Equal(1, block.ExpectedOffset);
        Assert.Equal(0, block.IgnoreReferenceCount);
    }

    [Fact]
    public void Diff_MultipleBlocks()
    {
        var diff = DiffGenerator.Default.Generate(Text9, Text10);

        Assert.NotNull(diff.Blocks);
        Assert.Equal(2, diff.Blocks.Count);

        var block1 = diff.Blocks[0];

        Assert.NotNull(block1.DeletedLines);
        Assert.Single(block1.DeletedLines);
        Assert.Equal("with a few lines in it", block1.DeletedLines[0]);
        Assert.NotNull(block1.AddedLines);
        Assert.Single(block1.AddedLines);
        Assert.Equal("with a few changed lines in it", block1.AddedLines[0]);

        Assert.Equal(DiffGenerator.StartLineReference, block1.Reference1);
        Assert.Equal("This is just some text", block1.Reference2);

        Assert.Equal(1, block1.ExpectedOffset);
        Assert.Equal(0, block1.IgnoreReferenceCount);

        var block2 = diff.Blocks[1];

        Assert.NotNull(block2.DeletedLines);
        Assert.Single(block2.DeletedLines);
        Assert.Equal("and there ends up being a difference", block2.DeletedLines[0]);
        Assert.NotNull(block2.AddedLines);
        Assert.Single(block2.AddedLines);
        Assert.Equal("and there ends up being a difference in multiple places", block2.AddedLines[0]);

        Assert.Equal("that just says basically nothing at all", block2.Reference1);
        Assert.Equal("but maybe just has a bit of a new thing", block2.Reference2);

        Assert.Equal(3, block2.ExpectedOffset);
        Assert.Equal(0, block2.IgnoreReferenceCount);
    }

    [Theory]
    [InlineData(Text1)]
    [InlineData(Text2)]
    [InlineData(Text9)]
    [InlineData(Text10)]
    [InlineData("some text")]
    [InlineData("some text\nother stuff")]
    [InlineData("some text\r\nother stuff")]
    [InlineData("some text\n\nother stuff\n")]
    public void Diff_EmptyDiffAppliesCorrectly(string text)
    {
        Assert.Equal(text, DiffGenerator.Default.ApplyDiff(text, new DiffData()).ToString());
        Assert.Equal(text, DiffGenerator.Default.ApplyDiff(text, new DiffData(new List<DiffData.Block>())).ToString());

        // Test also with a single pretty malformed block that doesn't have any operations
        Assert.Equal(text,
            DiffGenerator.Default.ApplyDiff(text,
                new DiffData([
                    new DiffData.Block(0,
                        0,
                        DiffGenerator.StartLineReference,
                        DiffGenerator.StartLineReference,
                        null,
                        null),
                ])).ToString());
    }

    [Fact]
    public void Diff_TrailingNewLineIsAppliedCorrectly()
    {
        var old = Text2;
        var updated = Text1;

        var diff = DiffGenerator.Default.Generate(old, updated);

        Assert.NotNull(diff.Blocks);
        Assert.Single(diff.Blocks);

        var block1 = diff.Blocks[0];

        Assert.Null(block1.AddedLines);
        Assert.NotNull(block1.DeletedLines);

        Assert.Equal(2, block1.DeletedLines.Count);
        Assert.Equal("but maybe just has a bit of a new thing", block1.DeletedLines[0]);
        Assert.Empty(block1.DeletedLines[1]);

        Assert.Equal(updated, DiffGenerator.Default.ApplyDiff(old, diff).ToString());

        old = Text2 + "\n";
        diff = DiffGenerator.Default.Generate(old, updated);

        Assert.Equal(updated, DiffGenerator.Default.ApplyDiff(old, diff).ToString());
    }

    [Theory]
    [InlineData(Text1, Text2)]
    [InlineData(Text1, "")]
    [InlineData("", Text1)]
    [InlineData("", Text1 + "\n")]
    [InlineData(Text5, Text6)]
    [InlineData(Text1, Text7)]
    [InlineData(Text9, Text10)]
    [InlineData(Text1, Text11)]
    public void Diff_GeneratedDiffWhenAppliedGivesNewText(string old, string updated)
    {
        var diff = DiffGenerator.Default.Generate(old, updated);

        var result = DiffGenerator.Default.ApplyDiff(old, diff);

        Assert.Equal(updated, result.ToString());
    }

    [Theory]
    [InlineData(Text1, Text2)]
    [InlineData(Text1, "")]
    [InlineData("", Text1)]
    [InlineData(Text9, Text10)]
    public void Diff_ReverseDiffApplyWorks(string old, string updated)
    {
        var diff = DiffGenerator.Default.Generate(updated, old);

        var result = DiffGenerator.Default.ApplyDiff(updated, diff);

        Assert.Equal(old, result.ToString());
    }

    [Fact]
    public void Diff_ReferenceSkippingWorks()
    {
        var diff = DiffGenerator.Default.Generate(Text8, Text9);

        Assert.NotNull(diff.Blocks);
        Assert.Single(diff.Blocks);

        var block = diff.Blocks.First();

        Assert.NotNull(block.DeletedLines);
        Assert.Single(block.DeletedLines);
        Assert.Equal("but only after a changed line", block.DeletedLines[0]);
        Assert.NotNull(block.AddedLines);
        Assert.Single(block.AddedLines);
        Assert.Equal("that might be tricky to match", block.AddedLines[0]);

        Assert.Equal("and there ends up being a difference", block.Reference1);
        Assert.Equal("that is after a reference match", block.Reference2);

        Assert.Equal(7, block.ExpectedOffset);
        Assert.Equal(1, block.IgnoreReferenceCount);

        var result = DiffGenerator.Default.ApplyDiff(Text8, diff);

        Assert.Equal(Text9, result.ToString());
    }

    [Theory]
    [InlineData(Text1, Text2)]
    [InlineData(Text1, "")]
    [InlineData("", Text1)]
    public void Diff_RoundTripThroughJsonWorks(string old, string updated)
    {
        var diff = DiffGenerator.Default.Generate(old, updated);
        Assert.NotNull(diff.Blocks);

        var encoded = JsonSerializer.Serialize(diff);

        var restored = JsonSerializer.Deserialize<DiffData>(encoded);

        Assert.NotNull(restored);

        Assert.NotNull(restored.Blocks);
        Assert.Equal(diff.Blocks.Count, restored.Blocks.Count);

        for (int i = 0; i < diff.Blocks.Count; ++i)
        {
            Assert.Equal(diff.Blocks[i], restored.Blocks[i]);
        }

        var result = DiffGenerator.Default.ApplyDiff(old, restored);

        Assert.Equal(updated, result.ToString());
    }

    [Fact]
    public void Diff_BlankDiffCanBeLoaded()
    {
        var restored = JsonSerializer.Deserialize<DiffData>("{}");
        Assert.NotNull(restored);
        Assert.True(restored.Empty);
    }

    [Theory]
    [InlineData(Text1, Text2)]
    [InlineData(Text1, "")]
    [InlineData("", Text1)]
    [InlineData(Text5, Text6)]
    [InlineData(Text1, Text7)]
    [InlineData(Text9, Text10)]
    [InlineData(Text1, Text11)]
    public void Diff_WindowsStyleLineEndings(string old, string updated)
    {
        if (!old.Contains("\n") && !updated.Contains("\n"))
        {
            throw new ArgumentException("Text to test with should have newlines");
        }

        old = old.Replace("\n", "\r\n");
        updated = updated.Replace("\n", "\r\n");

        var diff = DiffGenerator.Default.Generate(old, updated);
        Assert.NotNull(diff.Blocks);

        var encoded = JsonSerializer.Serialize(diff);

        var restored = JsonSerializer.Deserialize<DiffData>(encoded);

        Assert.NotNull(restored);

        Assert.NotNull(restored.Blocks);
        Assert.Equal(diff.PreferWindowsLineEndings, restored.PreferWindowsLineEndings);

        var result = DiffGenerator.Default.ApplyDiff(old, restored);

        Assert.Equal(updated, result.ToString());
    }

    [Theory]
    [InlineData(SpecificText1Old, SpecificText1New)]
    public void Diff_SpecificProblematicTextsWork(string old, string updated)
    {
        var diff = DiffGenerator.Default.Generate(old, updated);

        var result = DiffGenerator.Default.ApplyDiff(old, diff);

        Assert.Equal(updated, result.ToString());
    }
}
