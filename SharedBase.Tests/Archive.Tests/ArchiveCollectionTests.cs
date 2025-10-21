namespace SharedBase.Tests.Archive.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        var result = reader.ReadObjectOrNull<ITuple>();

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
            result = ((int, string, bool))reader.ReadObjectOrNull(out _)!);
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

        Assert.Throws<InvalidCastException>(() => read = (Tuple<int, string, bool>)reader.ReadObjectOrNull<ITuple>()!);

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
        customManager.RegisterObjectType(ArchiveObjectType.TestObjectType1, typeof(ArchiveObjectTests.TestObject1),
            ArchiveObjectTests.TestObject1.WriteToArchive);
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
        customManager.RegisterObjectType(ArchiveObjectType.TestObjectType1, typeof(ArchiveObjectTests.TestObject4),
            ArchiveObjectTests.TestObject4.WriteToArchive);
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

    [Fact]
    public void ArchiveCollection_SimpleList()
    {
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, manager);
        var reader = new SArchiveMemoryReader(memoryStream, manager);

        var original = new List<int> { 1, 2, 34, 56, 90 };

        writer.WriteObject(original);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read = reader.ReadObjectOrNull<List<int>>();

        Assert.NotNull(read);
        Assert.True(original.SequenceEqual(read));

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read2 = (List<int>?)reader.ReadObjectOrNull(out _);
        Assert.NotNull(read2);
        Assert.True(original.SequenceEqual(read2));
    }

    [Fact]
    public void ArchiveCollection_EmptyList()
    {
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, manager);
        var reader = new SArchiveMemoryReader(memoryStream, manager);

        // Intentionally empty list
        // ReSharper disable once CollectionNeverUpdated.Local
        var original = new List<int>();

        writer.WriteObject(original);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read = reader.ReadObjectOrNull<List<int>>();

        Assert.NotNull(read);
        Assert.Equal(original.Count, read.Count);
        Assert.True(original.SequenceEqual(read));
    }

    [Fact]
    public void ArchiveCollection_TupleInEmptyList()
    {
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, manager);
        var reader = new SArchiveMemoryReader(memoryStream, manager);

        // Intentionally empty list
        // ReSharper disable once CollectionNeverUpdated.Local
        var original = new List<(string Tag, int Value)>();

        writer.WriteObject(original);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read = reader.ReadObjectOrNull<List<(string Tag, int Value)>>();
        Assert.NotNull(read);
        Assert.Equal(original.Count, read.Count);
        Assert.True(original.SequenceEqual(read));
    }

    [Fact]
    public void ArchiveCollection_PrimitiveListTypes()
    {
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, manager);
        var reader = new SArchiveMemoryReader(memoryStream, manager);

        // Bool test
        var original1 = new List<bool> { true, false, false, true, true };

        writer.WriteObject(original1);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read1 = reader.ReadObjectOrNull<List<bool>>();

        Assert.NotNull(read1);
        Assert.True(original1.SequenceEqual(read1));

        // Long test
        memoryStream.Seek(0, SeekOrigin.Begin);

        var original2 = new List<long> { 1, 4, 123, 4356364, long.MaxValue, long.MinValue };

        writer.WriteObject(original2);
        memoryStream.Seek(0, SeekOrigin.Begin);
        var read2 = reader.ReadObjectOrNull<List<long>>();
        Assert.NotNull(read2);
        Assert.True(original2.SequenceEqual(read2));

        // String test
        memoryStream.Seek(0, SeekOrigin.Begin);

        var original3 = new List<string> { "hello", "world", "this", "is", "a", "test" };

        writer.WriteObject(original3);

        memoryStream.Seek(0, SeekOrigin.Begin);
        var read3 = reader.ReadObjectOrNull<List<string>>();
        Assert.NotNull(read3);
        Assert.True(original3.SequenceEqual(read3));

        // Float test
        memoryStream.Seek(0, SeekOrigin.Begin);

        var original4 = new List<float>
            { 1.23f, 4.56f, 7.89f, 10.11f, 12.13f, float.NaN, float.MaxValue, float.NegativeInfinity };

        writer.WriteObject(original4);

        memoryStream.Seek(0, SeekOrigin.Begin);
        var read4 = reader.ReadObjectOrNull<List<float>>();
        Assert.NotNull(read4);
        Assert.True(original4.SequenceEqual(read4));
    }

    [Fact]
    public void ArchiveCollection_PrimitiveWriteBenefitsFromDataEfficiency()
    {
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, manager);
        var reader = new SArchiveMemoryReader(memoryStream, manager);

        var original = new List<byte>
        {
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24,
            50, 55, 100, 150, 200, 255,
        };

        writer.WriteObject(original);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read = reader.ReadObjectOrNull<List<byte>>();

        Assert.NotNull(read);
        Assert.True(original.SequenceEqual(read));

        Assert.True(memoryStream.Length <= original.Count * sizeof(byte) + 16);
    }

    [Fact]
    public void ArchiveCollection_SimpleArray()
    {
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, manager);
        var reader = new SArchiveMemoryReader(memoryStream, manager);

        var original = new[] { 1, 2, 34, 56, 90 };

        writer.WriteObject(original);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read = reader.ReadObjectOrNull<int[]>();

        Assert.NotNull(read);
        Assert.Equal(original.Length, read.Length);
        Assert.True(original.SequenceEqual(read));

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read2 = (int[]?)reader.ReadObjectOrNull(out _);
        Assert.NotNull(read2);
        Assert.True(original.SequenceEqual(read2));
    }

    [Fact]
    public void ArchiveCollection_UnOptimizedArray()
    {
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, manager);
        var reader = new SArchiveMemoryReader(memoryStream, manager);

        var original = new ushort[] { 1, 2, 34, 56, 90 };

        writer.WriteObject(original);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read = reader.ReadObjectOrNull<ushort[]>();

        Assert.NotNull(read);
        Assert.Equal(original.Length, read.Length);
        Assert.True(original.SequenceEqual(read));

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read2 = (ushort[]?)reader.ReadObjectOrNull(out _);
        Assert.NotNull(read2);
        Assert.True(original.SequenceEqual(read2));

        // Check memory use to confirm unoptimised use
        Assert.True(memoryStream.Length > original.Length * sizeof(ushort) * 2 + 16);
    }

    [Fact]
    public void ArchiveCollection_NestedListTest()
    {
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, manager);
        var reader = new SArchiveMemoryReader(memoryStream, manager);

        var original = new List<List<int>> { new() { 1, 2 }, new() { 34, 56, 90 } };

        writer.WriteObject(original);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read = reader.ReadObjectOrNull<List<List<int>>>();

        Assert.NotNull(read);

        Assert.Equal(original.Count, read.Count);

        for (int i = 0; i < original.Count; ++i)
        {
            Assert.True(original[i].SequenceEqual(read[i]));
        }

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read2 = (List<List<int>>?)reader.ReadObjectOrNull(out _);
        Assert.NotNull(read2);
        Assert.Equal(original.Count, read2.Count);
    }

    [Fact]
    public void ArchiveCollection_ListWithCustomClassType()
    {
        var customManager = new DefaultArchiveManager(true);
        customManager.RegisterObjectType(ArchiveObjectType.TestObjectType1, typeof(ArchiveObjectTests.TestObject1),
            ArchiveObjectTests.TestObject1.WriteToArchive);
        customManager.RegisterObjectType(ArchiveObjectType.TestObjectType1, typeof(ArchiveObjectTests.TestObject1),
            ArchiveObjectTests.TestObject1.ReadFromArchive);
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, customManager);
        var reader = new SArchiveMemoryReader(memoryStream, customManager);

        var original = new List<ArchiveObjectTests.TestObject1>
        {
            new()
            {
                Value1 = 12,
                Value2 = 0,
                Value3 = "A test string!",
                Value4 = true,
            },
        };

        writer.WriteObject(original);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read = reader.ReadObjectOrNull<List<ArchiveObjectTests.TestObject1>>();

        Assert.NotNull(read);
        Assert.True(original.SequenceEqual(read));
    }

    [Fact]
    public void ArchiveCollection_BasicDictionary()
    {
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, manager);
        var reader = new SArchiveMemoryReader(memoryStream, manager);

        var original = new Dictionary<string, int>
        {
            { "item1", 1 },
            { "some other item", 2 },
            { "item3", 3 },
            { "item4", 4 },
            { "item5", 5 },
        };

        writer.WriteObject(original);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read = reader.ReadObjectOrNull<Dictionary<string, int>>();

        Assert.NotNull(read);
        Assert.Equal(original.Count, read.Count);

        // Efficiency won't matter here, just knowing if things match
        // ReSharper disable once UsageOfDefaultStructEquality
        Assert.True(original.SequenceEqual(read));
    }

    [Fact]
    public void ArchiveCollection_EmptyDictionary()
    {
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, manager);
        var reader = new SArchiveMemoryReader(memoryStream, manager);

        // Intentionally empty dictionary
        // ReSharper disable once CollectionNeverUpdated.Local
        var original = new Dictionary<string, int>();

        writer.WriteObject(original);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read = reader.ReadObjectOrNull<Dictionary<string, int>>();

        Assert.NotNull(read);
        Assert.Equal(original.Count, read.Count);

        // Efficiency won't matter here, just knowing if things match
        // ReSharper disable once UsageOfDefaultStructEquality
        Assert.True(original.SequenceEqual(read));
    }

    [Fact]
    public void ArchiveCollection_DictionaryWithCustomClassKey()
    {
        var customManager = new DefaultArchiveManager(true);
        customManager.RegisterObjectType(ArchiveObjectType.TestObjectType1, typeof(ArchiveObjectTests.TestObject1),
            ArchiveObjectTests.TestObject1.WriteToArchive);
        customManager.RegisterObjectType(ArchiveObjectType.TestObjectType1, typeof(ArchiveObjectTests.TestObject1),
            ArchiveObjectTests.TestObject1.ReadFromArchive);
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, customManager);
        var reader = new SArchiveMemoryReader(memoryStream, customManager);

        var original = new Dictionary<ArchiveObjectTests.TestObject1, string>
        {
            {
                new ArchiveObjectTests.TestObject1
                {
                    Value1 = 12,
                    Value2 = 0,
                    Value3 = "A test string!",
                    Value4 = true,
                },
                "important details!"
            },
        };

        writer.WriteObject(original);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read = reader.ReadObjectOrNull<Dictionary<ArchiveObjectTests.TestObject1, string>>();

        Assert.NotNull(read);
        Assert.Equal(original.Count, read.Count);

        // Efficiency won't matter here, just knowing if things match
        // ReSharper disable once UsageOfDefaultStructEquality
        Assert.True(original.SequenceEqual(read));
    }

    [Fact]
    public void ArchiveCollection_DictionaryWithCustomClassValue()
    {
        var customManager = new DefaultArchiveManager(true);
        customManager.RegisterObjectType(ArchiveObjectType.TestObjectType1, typeof(ArchiveObjectTests.TestObject1),
            ArchiveObjectTests.TestObject1.WriteToArchive);
        customManager.RegisterObjectType(ArchiveObjectType.TestObjectType1, typeof(ArchiveObjectTests.TestObject1),
            ArchiveObjectTests.TestObject1.ReadFromArchive);
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, customManager);
        var reader = new SArchiveMemoryReader(memoryStream, customManager);

        var original = new Dictionary<string, ArchiveObjectTests.TestObject1>
        {
            {
                "item1",
                new ArchiveObjectTests.TestObject1
                {
                    Value1 = 12,
                    Value2 = 0,
                    Value3 = string.Empty,
                    Value4 = true,
                }
            },
        };

        writer.WriteObject(original);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read = reader.ReadObjectOrNull<Dictionary<string, ArchiveObjectTests.TestObject1>>();

        Assert.NotNull(read);
        Assert.Equal(original.Count, read.Count);

        // Efficiency won't matter here, just knowing if things match
        // ReSharper disable once UsageOfDefaultStructEquality
        Assert.True(original.SequenceEqual(read));

        Assert.True(read.ContainsKey("item1"));
    }

    [Fact]
    public void ArchiveCollection_DictionaryWithCustomClassKeyAndValue()
    {
        var customManager = new DefaultArchiveManager(true);
        customManager.RegisterObjectType(ArchiveObjectType.TestObjectType1, typeof(ArchiveObjectTests.TestObject1),
            ArchiveObjectTests.TestObject1.WriteToArchive);
        customManager.RegisterObjectType(ArchiveObjectType.TestObjectType1, typeof(ArchiveObjectTests.TestObject1),
            ArchiveObjectTests.TestObject1.ReadFromArchive);

        customManager.RegisterObjectType(ArchiveObjectType.TestObjectType2, typeof(ArchiveObjectTests.TestObject5),
            ArchiveObjectTests.TestObject5.WriteToArchive);
        customManager.RegisterBoxableValueType(ArchiveObjectType.TestObjectType2,
            typeof(ArchiveObjectTests.TestObject5),
            ArchiveObjectTests.TestObject5.ConstructBoxedArchiveRead);

        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, customManager);
        var reader = new SArchiveMemoryReader(memoryStream, customManager);

        var keyObject = new ArchiveObjectTests.TestObject1
        {
            Value1 = 12,
            Value2 = 0,
            Value3 = "A test string!",
            Value4 = true,
        };

        var original = new Dictionary<ArchiveObjectTests.TestObject1, ArchiveObjectTests.TestObject5>
        {
            {
                keyObject,
                new ArchiveObjectTests.TestObject5
                {
                    Value1 = 1,
                    Value2 = 2,
                    Value3 = "third",
                    Value4 = true,
                }
            },
        };

        writer.WriteObject(original);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read = reader
            .ReadObjectOrNull<Dictionary<ArchiveObjectTests.TestObject1, ArchiveObjectTests.TestObject5>>();

        Assert.NotNull(read);
        Assert.Equal(original.Count, read.Count);

        // Efficiency won't matter here, just knowing if things match
        // ReSharper disable once UsageOfDefaultStructEquality
        Assert.True(original.SequenceEqual(read));

        Assert.True(read.ContainsKey(keyObject));
    }

    [Fact]
    public void ArchiveCollection_DictionaryWithTupleKey()
    {
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, manager);
        var reader = new SArchiveMemoryReader(memoryStream, manager);

        var key1 = ("some text", 1, false);
        var key2 = ("tag", 2, true);

        var original = new Dictionary<(string Tag, int Value, bool Indicator), string>
        {
            {
                key1, "aa"
            },
            {
                key2, "some data"
            },
        };

        writer.WriteObject(original);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read = reader.ReadObjectOrNull<Dictionary<(string Tag, int Value, bool Indicator), string>>();

        Assert.NotNull(read);
        Assert.Equal(original.Count, read.Count);
        Assert.NotEmpty(read);

        // Efficiency won't matter here, just knowing if things match
        // ReSharper disable once UsageOfDefaultStructEquality
        Assert.True(original.SequenceEqual(read));
    }

    [Fact]
    public void ArchiveCollection_DictionaryWithTupleKeyThatIsEmpty()
    {
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, manager);
        var reader = new SArchiveMemoryReader(memoryStream, manager);

        // Intentionally empty dictionary
        // ReSharper disable once CollectionNeverUpdated.Local
        var original = new Dictionary<(string Tag, int Value, bool Indicator), string>();

        writer.WriteObject(original);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read = reader.ReadObjectOrNull<Dictionary<(string Tag, int Value, bool Indicator), string>>();

        Assert.NotNull(read);
        Assert.Equal(original.Count, read.Count);

        // Efficiency won't matter here, just knowing if things match
        // ReSharper disable once UsageOfDefaultStructEquality
        Assert.True(original.SequenceEqual(read));
    }

    [Fact]
    public void ArchiveCollection_NestedDictionaryWithDictionariesAndLists()
    {
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, manager);
        var reader = new SArchiveMemoryReader(memoryStream, manager);

        var original = new Dictionary<string, Dictionary<string, List<int>>>
        {
            {
                "thing", new Dictionary<string, List<int>>
                {
                    { "second value", [2, 4] },
                    { "more nested stuff", [50] },
                }
            },
            {
                "thing2", new Dictionary<string, List<int>> { { "stuff", [5] } }
            },
        };

        writer.WriteObject(original);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read = reader.ReadObjectOrNull<Dictionary<string, Dictionary<string, List<int>>>>();

        Assert.NotNull(read);
        Assert.Equal(original.Count, read.Count);
        Assert.NotEmpty(read);

        Assert.NotEmpty(read["thing"]);
        Assert.True(read["thing"].ContainsKey("second value"));
        Assert.True(read["thing"].ContainsKey("more nested stuff"));
        Assert.NotEmpty(read["thing2"]);
        Assert.True(read["thing2"].ContainsKey("stuff"));

        Assert.True(original["thing"]["second value"].SequenceEqual(read["thing"]["second value"]));
        Assert.True(original["thing"]["more nested stuff"].SequenceEqual(read["thing"]["more nested stuff"]));
        Assert.True(original["thing2"]["stuff"].SequenceEqual(read["thing2"]["stuff"]));
    }

    [Fact]
    public void ArchiveCollection_NestedDictionaryWithDictionariesAndListsAndCustomClass()
    {
        var customManager = new DefaultArchiveManager(true);
        customManager.RegisterObjectType(ArchiveObjectType.TestObjectType1, typeof(ArchiveObjectTests.TestObject1),
            ArchiveObjectTests.TestObject1.WriteToArchive);
        customManager.RegisterObjectType(ArchiveObjectType.TestObjectType1, typeof(ArchiveObjectTests.TestObject1),
            ArchiveObjectTests.TestObject1.ReadFromArchive);

        customManager.RegisterObjectType(ArchiveObjectType.TestObjectType2, typeof(ArchiveObjectTests.TestObject5),
            ArchiveObjectTests.TestObject5.WriteToArchive);
        customManager.RegisterBoxableValueType(ArchiveObjectType.TestObjectType2,
            typeof(ArchiveObjectTests.TestObject5),
            ArchiveObjectTests.TestObject5.ConstructBoxedArchiveRead);

        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, customManager);
        var reader = new SArchiveMemoryReader(memoryStream, customManager);

        var keyObject = new ArchiveObjectTests.TestObject1
        {
            Value1 = 12,
            Value2 = 0,
            Value3 = "A test string!",
            Value4 = true,
        };

        var innerKey = new ArchiveObjectTests.TestObject5
        {
            Value1 = 1,
            Value2 = 2,
            Value3 = "third",
            Value4 = true,
        };

        List<int> innerSequence = [25, 28];

        var original =
            new Dictionary<ArchiveObjectTests.TestObject1, Dictionary<ArchiveObjectTests.TestObject5, List<int>>>
            {
                {
                    keyObject,
                    new Dictionary<ArchiveObjectTests.TestObject5, List<int>>
                    {
                        {
                            innerKey,
                            innerSequence
                        },
                    }
                },
            };

        writer.WriteObject(original);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read = reader
            .ReadObjectOrNull<Dictionary<ArchiveObjectTests.TestObject1,
                Dictionary<ArchiveObjectTests.TestObject5, List<int>>>>();

        Assert.NotNull(read);
        Assert.Equal(original.Count, read.Count);

        Assert.True(read.ContainsKey(keyObject));
        Assert.True(read[keyObject].ContainsKey(innerKey));

        Assert.True(read[keyObject][innerKey].SequenceEqual(innerSequence));
    }

    [Fact]
    public void ArchiveCollection_NestedDictionaryWithDictionariesAndListsAndCustomClassThatIsEmpty()
    {
        var customManager = new DefaultArchiveManager(true);
        customManager.RegisterObjectType(ArchiveObjectType.TestObjectType1, typeof(ArchiveObjectTests.TestObject1),
            ArchiveObjectTests.TestObject1.WriteToArchive);
        customManager.RegisterObjectType(ArchiveObjectType.TestObjectType1, typeof(ArchiveObjectTests.TestObject1),
            ArchiveObjectTests.TestObject1.ReadFromArchive);

        customManager.RegisterObjectType(ArchiveObjectType.TestObjectType2, typeof(ArchiveObjectTests.TestObject5),
            ArchiveObjectTests.TestObject5.WriteToArchive);
        customManager.RegisterBoxableValueType(ArchiveObjectType.TestObjectType2,
            typeof(ArchiveObjectTests.TestObject5),
            ArchiveObjectTests.TestObject5.ConstructBoxedArchiveRead);

        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, customManager);
        var reader = new SArchiveMemoryReader(memoryStream, customManager);

        // Intentionally empty dictionary
        // ReSharper disable once CollectionNeverUpdated.Local
        var original =
            new Dictionary<ArchiveObjectTests.TestObject1, Dictionary<ArchiveObjectTests.TestObject5, List<int>>>();

        writer.WriteObject(original);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read = reader
            .ReadObjectOrNull<Dictionary<ArchiveObjectTests.TestObject1,
                Dictionary<ArchiveObjectTests.TestObject5, List<int>>>>();

        Assert.NotNull(read);
        Assert.Equal(original.Count, read.Count);

        // Efficiency won't matter here, just knowing if things match
        // ReSharper disable once UsageOfDefaultStructEquality
        Assert.True(original.SequenceEqual(read));

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read2 = reader.ReadObjectOrNull(out var type);
        Assert.Equal(ArchiveObjectType.ExtendedDictionary, type);
        Assert.IsType(original.GetType(), read2);
    }

    [Fact]
    public void ArchiveCollection_ListOfDictionaries()
    {
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, manager);
        var reader = new SArchiveMemoryReader(memoryStream, manager);

        var original = new List<Dictionary<string, int>>
        {
            new()
            {
                { "thing", 1 },
            },
        };

        writer.WriteObject(original);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read = reader.ReadObjectOrNull<List<Dictionary<string, int>>>();

        Assert.NotNull(read);
        Assert.Equal(original.Count, read.Count);
        Assert.NotEmpty(read);

        // Efficiency won't matter here, just knowing if things match
        // ReSharper disable once UsageOfDefaultStructEquality
        Assert.True(original[0].SequenceEqual(read[0]));
    }

    [Fact]
    public void ArchiveCollection_ListOfDictionariesThatIsEmpty()
    {
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, manager);
        var reader = new SArchiveMemoryReader(memoryStream, manager);

        var original = new List<Dictionary<string, int>>();

        writer.WriteObject(original);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read = reader.ReadObjectOrNull<List<Dictionary<string, int>>>();

        Assert.NotNull(read);
        Assert.Equal(original.Count, read.Count);

        // Efficiency won't matter here, just knowing if things match
        // ReSharper disable once UsageOfDefaultStructEquality
        Assert.True(original.SequenceEqual(read));
    }

    [Fact]
    public void ArchiveCollection_HashSetSimple()
    {
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, manager);
        var reader = new SArchiveMemoryReader(memoryStream, manager);

        var original = new HashSet<int> { 1, 2, 3, 4, 5, 15 };

        writer.WriteGenericCollection(original);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read = reader.ReadObjectOrNull<List<int>>();

        Assert.NotNull(read);
        Assert.Equal(original.Count, read.Count);

        Assert.True(original.SetEquals(read));
    }

    [Fact]
    public void ArchiveCollection_GenericCollectionWrite()
    {
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, manager);
        var reader = new SArchiveMemoryReader(memoryStream, manager);

        var original = new HashSet<string>
            { "thing", "other", "thing2", "other stuff", "some final string that is a bit longer" };

        writer.WriteGenericCollection(original);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read = reader.ReadObjectOrNull<List<string>>();

        Assert.NotNull(read);
        Assert.Equal(original.Count, read.Count);

        Assert.True(original.SetEquals(read));
    }

    [Fact]
    public void ArchiveCollection_HashSetString()
    {
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, manager);
        var reader = new SArchiveMemoryReader(memoryStream, manager);

        var original = new HashSet<string>
            { "thing", "other", "thing2", "other stuff", "some final string that is a bit longer" };

        writer.WriteObject(original);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read = reader.ReadObjectOrNull<HashSet<string>>();

        Assert.NotNull(read);
        Assert.Equal(original.Count, read.Count);

        Assert.True(original.SetEquals(read));
    }

    [Fact]
    public void ArchiveCollection_DictionaryOfHashSets()
    {
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, manager);
        var reader = new SArchiveMemoryReader(memoryStream, manager);

        var original = new Dictionary<int, HashSet<string>>
        {
            {
                1,
                ["thing", "other", "thing2", "other stuff", "some final string that is a bit longer"]
            },
            {
                200,
                ["and stuff"]
            },
            {
                221,
                []
            },
        };

        writer.WriteObject(original);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read = reader.ReadObjectOrNull<Dictionary<int, HashSet<string>>>();

        Assert.NotNull(read);
        Assert.Equal(original.Count, read.Count);
        Assert.Contains(1, read);
        Assert.Contains(200, read);
        Assert.Contains(221, read);

        Assert.True(original[1].SetEquals(read[1]));
        Assert.True(original[200].SetEquals(read[200]));
        Assert.True(original[221].SetEquals(read[221]));
    }

    [Fact]
    public void ArchiveCollection_HashSetObject()
    {
        var customManager = new DefaultArchiveManager(true);
        customManager.RegisterObjectType(ArchiveObjectType.TestObjectType2, typeof(ArchiveObjectTests.TestObject5),
            ArchiveObjectTests.TestObject5.WriteToArchive);
        customManager.RegisterBoxableValueType(ArchiveObjectType.TestObjectType2,
            typeof(ArchiveObjectTests.TestObject5),
            ArchiveObjectTests.TestObject5.ConstructBoxedArchiveRead);

        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, customManager);
        var reader = new SArchiveMemoryReader(memoryStream, customManager);

        var thing1 = new ArchiveObjectTests.TestObject5
        {
            Value1 = 1,
            Value2 = 2,
            Value3 = "third",
            Value4 = true,
        };

        var thing2 = new ArchiveObjectTests.TestObject5
        {
            Value1 = 2,
            Value2 = 0,
            Value3 = string.Empty,
            Value4 = false,
        };

        var notInSet = new ArchiveObjectTests.TestObject5
        {
            Value1 = 3,
            Value2 = 1,
            Value3 = "third",
            Value4 = true,
        };

        var original = new HashSet<ArchiveObjectTests.TestObject5> { thing1, thing2 };

        writer.WriteGenericCollection(original);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read = reader.ReadObjectOrNull<List<ArchiveObjectTests.TestObject5>>();

        Assert.NotNull(read);
        Assert.Equal(original.Count, read.Count);

        Assert.True(original.SetEquals(read));

        var asSet = read.ToHashSet();

        Assert.True(original.SetEquals(asSet));

        Assert.Contains(thing1, asSet);
        Assert.Contains(thing2, asSet);
        Assert.DoesNotContain(notInSet, asSet);
    }
}
