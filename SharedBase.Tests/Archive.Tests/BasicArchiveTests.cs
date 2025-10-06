namespace SharedBase.Tests.Archive.Tests;

using SharedBase.Archive;
using Xunit;

public class BasicArchiveTests
{
    [Fact]
    public void BasicArchive_IntWritingAndReading()
    {
        var manager = new DefaultArchiveManager();
        var archive = new SArchiveMemory(manager, manager);

        archive.Write(1);

        archive.Seek(0);
        Assert.Equal(1, archive.ReadInt32());
    }
}
