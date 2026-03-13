namespace SharedBase.Tests.Utilities.Tests;

using System;
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

    private const string Text12 = """
                                  This is just some text
                                  with a few lines in it
                                  and there ends up being a difference
                                  but only after a changed line
                                  and ends with a match
                                  """;

    private const string Text13 = """
                                  This is just some text
                                  with a few lines in it
                                  and there ends up being a difference
                                  that just says basically nothing at all
                                  but maybe just has a bit of a new thing
                                  and there ends up being a difference
                                  """;

    private const string Text14 = """
                                  and there ends up being a difference
                                  that just says basically nothing at all
                                  but maybe just has a bit of a new thing
                                  and there ends up being a difference
                                  that is after a reference match
                                  but only after a changed line
                                  and ends with a match
                                  """;

    private const string Text15 = """
                                  This is just some text
                                  with a few lines in it
                                  (start)
                                  but maybe just has a bit of a new thing
                                  """;

    private const string Text16 = """
                                  This is just some text
                                  with a few lines in it
                                  (start)
                                  but maybe just has a bit of a new thing that changed
                                  """;

    private const string Text17 = """
                                  (start)
                                  with a few lines in it
                                  that just says basically nothing at all
                                  but maybe just has a bit of a new thing
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

    private const string SpecificText2Old = """

                                            TODO: new banner image
                                            [center]![banner_50.webp](media:webp:86890a33-2956-48ee-97b1-23fae2eadd8a)[/center]

                                            TODO: write some intro paragraphs
                                            """;

    private const string SpecificText2New = """

                                            TODO: new banner image
                                            [center]![banner_50.webp](media:webp:86890a33-2956-48ee-97b1-23fae2eadd8a)[/center]

                                            It's finally time for the first major update of the Multicellular development process!

                                            TODO: write some intro paragraphs
                                            """;

    [Fact]
    public void Diff_EmptyIsEmpty()
    {
        var diff = DiffGenerator.Default.Generate(Text1, Text1);

        Assert.True(diff.Empty);
        Assert.Null(diff.DiffDeltaRaw);
    }

    [Fact]
    public void Diff_WithJustNewText()
    {
        var diff = DiffGenerator.Default.Generate(string.Empty, Text1);

        Assert.False(diff.Empty);
        Assert.NotNull(diff.DiffDeltaRaw);

        Assert.Equal(Text1, DiffGenerator.Default.ApplyDiff(string.Empty, diff));
    }

    [Fact]
    public void Diff_WithJustOldText()
    {
        var diff = DiffGenerator.Default.Generate(Text1, string.Empty);

        Assert.False(diff.Empty);
        Assert.NotNull(diff.DiffDeltaRaw);

        Assert.Equal(string.Empty, DiffGenerator.Default.ApplyDiff(Text1, diff));
    }

    [Fact]
    public void Diff_OneLineIsReplaced()
    {
        var diff = DiffGenerator.Default.Generate(Text3, Text4);

        Assert.False(diff.Empty);
        Assert.NotNull(diff.DiffDeltaRaw);

        Assert.Equal(Text4, DiffGenerator.Default.ApplyDiff(Text3, diff));
    }

    [Fact]
    public void Diff_SimpleAppendIsGenerated()
    {
        var diff = DiffGenerator.Default.Generate(Text1, Text2);

        Assert.False(diff.Empty);
        Assert.NotNull(diff.DiffDeltaRaw);

        var expectedDiff = "=85\t+%0abut maybe just has a bit of a new thing";

        Assert.Equal(expectedDiff, diff.DiffDeltaRaw);
    }

    [Fact]
    public void Diff_WithJustNewLineDifference()
    {
        var diff = DiffGenerator.Default.Generate(Text5, Text6);

        Assert.NotNull(diff.DiffDeltaRaw);

        Assert.Equal(Text6, DiffGenerator.Default.ApplyDiff(Text5, diff));
    }

    // TODO: test for applying the wrong way around

    [Fact]
    public void Diff_FirstLineDifference()
    {
        var diff = DiffGenerator.Default.Generate(Text1, Text7);

        Assert.NotNull(diff.DiffDeltaRaw);

        Assert.Equal(Text7, DiffGenerator.Default.ApplyDiff(Text1, diff));
    }

    [Fact]
    public void Diff_OneDeletedLine()
    {
        var diff = DiffGenerator.Default.Generate(Text1, Text11);

        Assert.NotNull(diff.DiffDeltaRaw);

        Assert.Equal(Text11, DiffGenerator.Default.ApplyDiff(Text1, diff));
    }

    [Fact]
    public void Diff_MultipleBlocks()
    {
        var diff = DiffGenerator.Default.Generate(Text9, Text10);

        Assert.NotNull(diff.DiffDeltaRaw);

        Assert.Equal(Text10, DiffGenerator.Default.ApplyDiff(Text9, diff));
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
        Assert.Equal(text, DiffGenerator.Default.ApplyDiff(text, new DiffData()));
        Assert.Equal(text, DiffGenerator.Default.ApplyDiff(text, new DiffData(string.Empty)));
    }

    [Fact]
    public void Diff_BadDiffThrows()
    {
        Assert.Throws<ArgumentException>(() => DiffGenerator.Default.ApplyDiff(Text1, new DiffData(" ")));
    }

    [Fact]
    public void Diff_TrailingNewLineIsAppliedCorrectly()
    {
        var old = Text2;
        var updated = Text1;

        var diff = DiffGenerator.Default.Generate(old, updated);

        Assert.NotNull(diff.DiffDeltaRaw);

        Assert.Equal(updated, DiffGenerator.Default.ApplyDiff(old, diff));

        old = Text2 + "\n";
        diff = DiffGenerator.Default.Generate(old, updated);

        Assert.Equal(updated, DiffGenerator.Default.ApplyDiff(old, diff));
    }

    [Fact]
    public void Diff_WithJustMultipleDeletedLinesInARow()
    {
        var old = Text8;
        var updated = Text14;

        var diff = DiffGenerator.Default.Generate(old, updated);

        Assert.Equal(updated, DiffGenerator.Default.ApplyDiff(old, diff));
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
    [InlineData(Text8, Text12)]
    [InlineData(Text8, Text13)]
    [InlineData(Text8, Text14)]
    [InlineData(" ", Text1)]
    [InlineData(" ", Text11)]
    [InlineData(" ", Text12)]
    [InlineData(" ", Text13)]
    [InlineData(" ", Text14)]
    [InlineData(Text1, " ")]
    [InlineData(Text11, " ")]
    [InlineData(Text12, " ")]
    [InlineData("", "")]
    [InlineData("", " ")]
    [InlineData(" ", "")]
    [InlineData(" Just some text", Text1)]
    public void Diff_GeneratedDiffWhenAppliedGivesNewText(string old, string updated)
    {
        var diff = DiffGenerator.Default.Generate(old, updated);

        Assert.Equal(updated, DiffGenerator.Default.ApplyDiff(old, diff));
    }

    [Fact]
    public void Diff_WorksFromJustASpaceOldText()
    {
        var old = " ";
        var newText = "This is just some text";

        var diff = DiffGenerator.Default.Generate(old, newText);

        var expectedDiff = "+This\t=1\t+is just some text";

        Assert.Equal(expectedDiff, diff.DiffDeltaRaw);

        var result = DiffGenerator.Default.ApplyDiff(old, diff);

        Assert.Equal(newText, result);
    }

    [Theory]
    [InlineData(Text1, Text2)]
    [InlineData(Text1, "")]
    [InlineData("", Text1)]
    [InlineData(Text9, Text10)]
    [InlineData(Text8, Text12)]
    [InlineData(" ", Text12)]
    public void Diff_ReverseDiffApplyWorks(string old, string updated)
    {
        var diff = DiffGenerator.Default.Generate(updated, old);

        Assert.Equal(old, DiffGenerator.Default.ApplyDiff(updated, diff));
    }

    [Fact]
    public void Diff_ReferenceSkippingWorks()
    {
        var diff = DiffGenerator.Default.Generate(Text8, Text9);

        Assert.NotNull(diff.DiffDeltaRaw);

        Assert.Equal(Text9, DiffGenerator.Default.ApplyDiff(Text8, diff));
    }

    [Theory]
    [InlineData(Text1, Text2)]
    [InlineData(Text1, "")]
    [InlineData("", Text1)]
    public void Diff_RoundTripThroughJsonWorks(string old, string updated)
    {
        var diff = DiffGenerator.Default.Generate(old, updated);
        Assert.NotNull(diff.DiffDeltaRaw);

        var encoded = JsonSerializer.Serialize(diff);

        var restored = JsonSerializer.Deserialize<DiffData>(encoded);

        Assert.NotNull(restored);

        Assert.NotNull(restored.DiffDeltaRaw);
        Assert.Equal(diff.DiffDeltaRaw.Length, restored.DiffDeltaRaw.Length);
        Assert.Equal(diff.DiffDeltaRaw, restored.DiffDeltaRaw);

        Assert.Equal(updated, DiffGenerator.Default.ApplyDiff(old, restored));
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
        Assert.NotNull(diff.DiffDeltaRaw);

        var encoded = JsonSerializer.Serialize(diff);

        var restored = JsonSerializer.Deserialize<DiffData>(encoded);

        Assert.NotNull(restored);

        Assert.NotNull(restored.DiffDeltaRaw);

        var result = DiffGenerator.Default.ApplyDiff(old, restored);

        Assert.Equal(updated, result);
    }

    [Theory]
    [InlineData(Text1, Text2 + "\n")]
    [InlineData(Text1 + "\n", Text2 + "\n")]
    [InlineData(Text1 + "\n", Text2)]
    [InlineData(Text1 + "\n", Text1)]
    [InlineData(Text1, Text1 + "\n")]
    public void Diff_LastLineChangedWithLineEndings(string old, string updated)
    {
        var diff = DiffGenerator.Default.Generate(old, updated);

        Assert.Equal(updated, DiffGenerator.Default.ApplyDiff(old, diff));
    }

    [Theory]
    [InlineData(Text15, Text16)]
    [InlineData(Text15, Text17)]
    [InlineData(Text17, Text15)]
    [InlineData(Text15, "")]
    [InlineData(Text16, "")]
    [InlineData(Text17, "")]
    [InlineData("", Text15)]
    [InlineData("", Text16)]
    [InlineData("", Text17)]
    public void Diff_LinesMatchingStartSpecialValueAreHandledCorrectly(string old, string updated)
    {
        // Note these no longer do anything but just for fun the test still exists as it shouldn't break on these
        var diff = DiffGenerator.Default.Generate(old, updated);

        Assert.Equal(updated, DiffGenerator.Default.ApplyDiff(old, diff));
    }

    [Fact]
    public void Diff_ApplyWithStartingNewLineWorks()
    {
        var old = "\nThis is a text\nWith many lines\n";
        var newText = "\nThis is a text\nAnd an added line!\nWith many lines\n";

        var diff = DiffGenerator.Default.Generate(old, newText);

        var result = DiffGenerator.Default.ApplyDiff(old, diff);

        Assert.Equal(newText, result);
    }

    [Theory]
    [InlineData(SpecificText1Old, SpecificText1New)]
    [InlineData(SpecificText1New, SpecificText1Old)]
    [InlineData(SpecificText2Old, SpecificText2New)]
    [InlineData(SpecificText2New, SpecificText2Old)]
    public void Diff_SpecificProblematicTextsWork(string old, string updated)
    {
        var diff = DiffGenerator.Default.Generate(old, updated);

        var result = DiffGenerator.Default.ApplyDiff(old, diff);

        Assert.Equal(updated, result);
    }

    [Fact]
    public void Diff_SpaceSavedComparedToFullText()
    {
        var old = Text12 + "\n" + Text13 + "\n" + Text14;

        var updated = old.Replace("but only after a changed line", "With a not replaced! line");

        var diff = DiffGenerator.Default.Generate(old, updated);

        Assert.NotNull(diff.DiffDeltaRaw);

        Assert.Equal(updated, DiffGenerator.Default.ApplyDiff(old, diff));

        Assert.True(diff.DiffDeltaRaw.Length < old.Length * 0.3f);
    }
}
