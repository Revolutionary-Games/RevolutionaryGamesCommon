namespace SharedBase.Tests.Utilities.Tests;

using SharedBase.Utilities;
using Xunit;

public class LineByLineReaderTests
{
    private const string Fragment1 = "Just some text";
    private const string Fragment2 = "that has multiple lines";
    private const string Fragment3 = "that ends just like this";

    private const string Text1 = Fragment1 + "\n" + Fragment2 + "\n" + Fragment3;

    private const string Text2 = Text1 + "\n";

    private const string WindowsText = Fragment1 + "\r\n" + Fragment2 + "\r\n" + Fragment3;

    [Fact]
    public void LineReader_ReadsSimpleCase()
    {
        var reader = new LineByLineReader(Text1);

        Assert.False(reader.Ended);
        Assert.False(reader.AtLineEnd);

        Assert.True(reader.LookForLineEnd());
        Assert.True(reader.AtLineEnd);
        Assert.Equal(Fragment1, reader.ReadCurrentLineToStart());
        Assert.Equal(0, reader.LineIndex);
        Assert.Equal(1, reader.LineNumber);
        reader.MoveToNextLine();
        Assert.Equal(1, reader.LineIndex);
        Assert.False(reader.AtLineEnd);

        Assert.True(reader.LookForLineEnd());
        Assert.Equal(1, reader.LineIndex);
        Assert.True(reader.AtLineEnd);
        Assert.Equal(Fragment2, reader.ReadCurrentLineToStart());
        reader.MoveToNextLine();
        Assert.Equal(2, reader.LineIndex);

        Assert.False(reader.LookForLineEnd());
        Assert.False(reader.AtLineEnd);
        Assert.False(reader.Ended);
        Assert.Equal(Fragment3, reader.ReadCurrentLineToStart());
        Assert.False(reader.AtLineEnd);
        Assert.False(reader.Ended);
        Assert.Equal(2, reader.LineIndex);

        Assert.False(reader.LookForLineEnd());
        Assert.True(reader.Ended);
    }

    [Fact]
    public void LineReader_ReadsWithEndingNewLine()
    {
        var reader = new LineByLineReader(Text2);

        Assert.True(reader.LookForLineEnd());
        Assert.Equal(Fragment1, reader.ReadCurrentLineToStart());
        reader.MoveToNextLine();

        Assert.True(reader.LookForLineEnd());
        Assert.Equal(Fragment2, reader.ReadCurrentLineToStart());
        reader.MoveToNextLine();

        Assert.True(reader.LookForLineEnd());
        Assert.Equal(Fragment3, reader.ReadCurrentLineToStart());
        reader.MoveToNextLine();

        Assert.False(reader.LookForLineEnd());
        Assert.False(reader.AtLineEnd);
        Assert.False(reader.Ended);

        Assert.Equal(string.Empty, reader.ReadCurrentLineToStart());

        Assert.False(reader.LookForLineEnd());
        Assert.True(reader.Ended);
        Assert.Equal(3, reader.LineIndex);
    }

    [Fact]
    public void LineReader_ReadsWindowsLineEndings()
    {
        var reader = new LineByLineReader(WindowsText);

        Assert.False(reader.Ended);
        Assert.False(reader.AtLineEnd);

        Assert.True(reader.LookForLineEnd());
        Assert.True(reader.AtLineEnd);
        Assert.Equal(Fragment1, reader.ReadCurrentLineToStart());
        reader.MoveToNextLine();
        Assert.False(reader.AtLineEnd);

        Assert.True(reader.LookForLineEnd());
        Assert.True(reader.AtLineEnd);
        Assert.Equal(Fragment2, reader.ReadCurrentLineToStart());
        reader.MoveToNextLine();

        Assert.False(reader.LookForLineEnd());
        Assert.False(reader.AtLineEnd);
        Assert.False(reader.Ended);
        Assert.Equal(Fragment3, reader.ReadCurrentLineToStart());
        Assert.False(reader.AtLineEnd);
        Assert.False(reader.Ended);

        Assert.False(reader.LookForLineEnd());
        Assert.True(reader.Ended);
    }

    [Fact]
    public void LineReader_EndingWindowsLine()
    {
        var reader = new LineByLineReader(Fragment1 + "\r\n");

        Assert.False(reader.Ended);
        Assert.False(reader.AtLineEnd);

        Assert.True(reader.LookForLineEnd());
        Assert.True(reader.AtLineEnd);
        Assert.Equal(Fragment1, reader.ReadCurrentLineToStart());
        reader.MoveToNextLine();
        Assert.False(reader.AtLineEnd);

        Assert.False(reader.LookForLineEnd());
        Assert.False(reader.AtLineEnd);
        Assert.Equal(string.Empty, reader.ReadCurrentLineToStart());

        Assert.False(reader.LookForLineEnd());
        Assert.True(reader.Ended);
    }

    [Fact]
    public void LineReader_CloningWorks()
    {
        var reader = new LineByLineReader(Text1);

        Assert.False(reader.Ended);
        Assert.False(reader.AtLineEnd);

        Assert.True(reader.LookForLineEnd());
        Assert.True(reader.AtLineEnd);

        var cloned = reader.Clone();
        Assert.True(cloned.AtLineEnd);

        Assert.Equal(Fragment1, reader.ReadCurrentLineToStart());
        Assert.Equal(Fragment1, cloned.ReadCurrentLineToStart());
        Assert.True(reader.CompareCurrentLineWith(cloned));
        var cloned2 = reader.Clone();
        Assert.True(cloned.CompareCurrentLineWith(cloned2));
        reader.MoveToNextLine();
        cloned.MoveToNextLine();
        Assert.False(cloned.CompareCurrentLineWith(cloned2));
        Assert.False(reader.AtLineEnd);
        Assert.False(cloned.AtLineEnd);

        Assert.True(reader.LookForLineEnd());
        reader.MoveToNextLine();

        cloned = reader.Clone();

        Assert.False(reader.LookForLineEnd());
        Assert.False(cloned.LookForLineEnd());
        Assert.Equal(reader.AtLineEnd, cloned.AtLineEnd);
        Assert.Equal(reader.Ended, cloned.Ended);
        Assert.Equal(reader.ReadCurrentLineToStart(), cloned.ReadCurrentLineToStart());
        Assert.Equal(reader.AtLineEnd, cloned.AtLineEnd);
        Assert.Equal(reader.Ended, cloned.Ended);

        Assert.False(reader.LookForLineEnd());
        Assert.True(reader.Ended);
        Assert.False(cloned.Ended);
        Assert.False(cloned.LookForLineEnd());
        Assert.True(cloned.Ended);
    }

    [Fact]
    public void LineReader_GoingBackLineWorks()
    {
        var reader = new LineByLineReader(Text1);

        reader.LookForLineEnd();
        reader.MoveToNextLine();
        reader.LookForLineEnd();
        reader.MoveToNextLine();

        var line = reader.ReadCurrentLineToStart();

        Assert.Equal(2, reader.LineIndex);

        Assert.True(reader.LookBackwardsForLineEnd());
        reader.MoveToPreviousLine();
        Assert.Equal(1, reader.LineIndex);
        Assert.NotEqual(line, reader.ReadCurrentLineToStart());

        Assert.True(reader.LookBackwardsForLineEnd());
        reader.MoveToPreviousLine();
        Assert.Equal(0, reader.LineIndex);
        Assert.NotEqual(line, reader.ReadCurrentLineToStart());

        Assert.False(reader.LookBackwardsForLineEnd());
    }
}
