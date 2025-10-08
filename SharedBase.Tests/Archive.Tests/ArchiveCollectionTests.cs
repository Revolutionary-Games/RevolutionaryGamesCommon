namespace SharedBase.Tests.Archive.Tests;

using System;
using System.IO;
using System.Runtime.CompilerServices;
using SharedBase.Archive;
using Xunit;

public class ArchiveCollectionTests
{
    // This has a shared state but tests probably only run one test case from a class at a time
    private readonly DefaultArchiveManager manager = new(true);

    [Fact]
    public void ArchiveCollection_SimpleValueTuple()
    {
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, manager);
        var reader = new SArchiveMemoryReader(memoryStream, manager);

        var original = (42, "stuff", true);

        writer.WriteObject(original);

        memoryStream.Seek(0, SeekOrigin.Begin);

        (int, string, bool) result = default;

        reader.ReadAnyStruct(ref result);
    }

    [Fact]
    public void ArchiveCollection_SimpleReferenceTuple()
    {
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, manager);
        var reader = new SArchiveMemoryReader(memoryStream, manager);

        var original = Tuple.Create(42, "stuff", true);

        writer.WriteObject(original);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var result = reader.ReadObject<ITuple>();

        Assert.NotNull(result);
        Assert.Equal(original, result);
    }
}
