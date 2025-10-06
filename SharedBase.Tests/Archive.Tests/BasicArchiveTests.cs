namespace SharedBase.Tests.Archive.Tests;

using System.IO;
using SharedBase.Archive;
using Xunit;

public class BasicArchiveTests
{
    [Fact]
    public void BasicArchive_IntWritingAndReading()
    {
        var manager = new DefaultArchiveManager();
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, manager);
        var reader = new SArchiveMemoryReader(memoryStream, manager);

        writer.Write(1);

        memoryStream.Seek(0, SeekOrigin.Begin);
        Assert.Equal(1, reader.ReadInt32());
    }
}
