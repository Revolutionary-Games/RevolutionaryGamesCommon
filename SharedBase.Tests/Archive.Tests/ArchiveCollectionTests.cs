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

        Assert.NotEqual(original, result);
        reader.ReadAnyStruct(ref result);

        Assert.Equal(original, result);
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

    [Fact]
    public void ArchiveCollection_MixingReferenceTupleThrows()
    {
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, manager);
        var reader = new SArchiveMemoryReader(memoryStream, manager);

        var original = Tuple.Create(42, "stuff", true);

        writer.WriteObject(original);

        memoryStream.Seek(0, SeekOrigin.Begin);

        (int, string, bool) result = default;

        Assert.Throws<FormatException>(() => reader.ReadAnyStruct(ref result));

        memoryStream.Seek(0, SeekOrigin.Begin);

        Assert.Throws<InvalidCastException>(() =>
            result = ((int, string, bool))reader.ReadObject(out _)!);
    }

    [Fact]
    public void ArchiveCollection_MixingValueTupleThrows()
    {
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, manager);
        var reader = new SArchiveMemoryReader(memoryStream, manager);

        var original = (42, "stuff", true);

        writer.WriteObject(original);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read = Tuple.Create(42, "stuff", true);

        Assert.Throws<InvalidCastException>(() => read = (Tuple<int, string, bool>)reader.ReadObject<ITuple>()!);

        _ = read;
    }

    [Fact]
    public void ArchiveCollection_NestedTuple()
    {
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, manager);
        var reader = new SArchiveMemoryReader(memoryStream, manager);

        var original = (42, ("nightmare", true));

        writer.WriteObject(original);

        memoryStream.Seek(0, SeekOrigin.Begin);

        (int, (string, bool)) result = default;

        Assert.NotEqual(original, result);
        reader.ReadAnyStruct(ref result);

        Assert.Equal(original, result);
    }

    [Fact]
    public void ArchiveCollection_TupleWithCustomClassType()
    {
        var customManager = new DefaultArchiveManager(true);
        customManager.RegisterObjectType(ArchiveObjectType.TestObjectType1, typeof(ArchiveObjectTests.TestObject1),
            ArchiveObjectTests.TestObject1.ReadFromArchive);
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, customManager);
        var reader = new SArchiveMemoryReader(memoryStream, customManager);

        var original = (42, new ArchiveObjectTests.TestObject1
        {
            Value1 = 1,
            Value2 = 2,
            Value3 = "hello",
            Value4 = false,
        });

        writer.WriteObject(original);

        memoryStream.Seek(0, SeekOrigin.Begin);

        (int, ArchiveObjectTests.TestObject1) result = default;

        Assert.NotEqual(original, result);
        reader.ReadAnyStruct(ref result);

        Assert.Equal(original, result);
    }

    [Fact]
    public void ArchiveCollection_TupleWithCustomStructType()
    {
        var customManager = new DefaultArchiveManager(true);
        customManager.RegisterBoxableValueType(ArchiveObjectType.TestObjectType1,
            typeof(ArchiveObjectTests.TestObject4), ArchiveObjectTests.TestObject4.ConstructBoxedArchiveRead);
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, customManager);
        var reader = new SArchiveMemoryReader(memoryStream, customManager);

        var original = (42, new ArchiveObjectTests.TestObject4
        {
            Value1 = 1,
            Value2 = 2,
            Value3 = "hello",
            Value4 = false,
        });

        writer.WriteObject(original);

        memoryStream.Seek(0, SeekOrigin.Begin);

        (int, ArchiveObjectTests.TestObject4) result = default;

        Assert.NotEqual(original, result);
        reader.ReadAnyStruct(ref result);

        Assert.Equal(original, result);
    }
}
