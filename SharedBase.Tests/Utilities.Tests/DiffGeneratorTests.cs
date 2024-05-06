namespace SharedBase.Tests.Utilities.Tests;

using System;
using System.Linq;
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

    // TODO: test that differs by trailing newline
    private const string Text5 = "Just some text";
    private const string Text6 = Text5 + "\n";

    // TODO: test that has changes at the start
    private const string Text7 = """
                                 First line difference in text
                                 with a few lines in it
                                 that just says basically nothing at all
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
        Assert.InRange(block.ExpectedOffset, 0, 2);
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
        Assert.InRange(block.ExpectedOffset, 2, 4);
        Assert.Equal(0, block.IgnoreReferenceCount);
    }

    [Theory]
    [InlineData(Text1, Text2)]
    [InlineData(Text1, "")]
    [InlineData("", Text1)]
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
    public void Diff_ReverseDiffApplyWorks(string old, string updated)
    {
        var diff = DiffGenerator.Default.Generate(updated, old);

        var result = DiffGenerator.Default.ApplyDiff(updated, diff);

        Assert.Equal(old, result.ToString());
    }
}
