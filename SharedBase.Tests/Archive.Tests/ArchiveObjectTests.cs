namespace SharedBase.Tests.Archive.Tests;

using System;
using System.IO;
using SharedBase.Archive;
using Xunit;

public class ArchiveObjectTests
{
    [Fact]
    public void ArchiveObject_SimpleSerialization()
    {
        var manager = new DefaultArchiveManager(false);
        manager.RegisterObjectType(ArchiveObjectType.TestObjectType1, TestObject1.ReadFromArchive);
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, manager);
        var reader = new SArchiveMemoryReader(memoryStream, manager);

        var testObject = new TestObject1
        {
            Value1 = 12,
            Value2 = 1,
            Value3 = "hello",
            Value4 = true,
        };

        writer.WriteObject(testObject);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read = reader.ReadObjectLowLevel();

        Assert.NotNull(read);
        Assert.Equal(testObject, read);
    }

    [Fact]
    public void ArchiveObject_NonReferenceObjectsAreNotGrouped()
    {
        var manager = new DefaultArchiveManager(false);
        manager.RegisterObjectType(ArchiveObjectType.TestObjectType1, TestObject1.ReadFromArchive);
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, manager);
        var reader = new SArchiveMemoryReader(memoryStream, manager);

        var testObject = new TestObject1
        {
            Value1 = float.NaN,
            Value2 = -42,
            Value3 = "hello, this is a much longer test string",
            Value4 = false,
        };

        manager.OnStartNewWrite(writer);
        writer.WriteObject(testObject);
        writer.WriteObject(testObject);
        manager.OnFinishWrite(writer);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read = reader.ReadObjectLowLevel();

        Assert.NotNull(read);
        Assert.Equal(testObject, read);

        var read2 = reader.ReadObjectLowLevel();

        Assert.NotNull(read2);
        Assert.Equal(testObject, read2);

        Assert.False(ReferenceEquals(testObject, read));
        Assert.False(ReferenceEquals(read, read2));
    }

    [Fact]
    public void ArchiveObject_ReferencesReferToEarlierObjects()
    {
        var manager = new DefaultArchiveManager(false);
        manager.RegisterObjectType(ArchiveObjectType.TestObjectType1, TestObject2.ReadFromArchive);
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, manager);
        var reader = new SArchiveMemoryReader(memoryStream, manager);

        var testObject = new TestObject2
        {
            Value1 = float.MinValue,
            Value2 = 65754,
            Value3 = "hello, this is a much longer test string",
            Value4 = false,
        };

        manager.OnStartNewWrite(writer);
        writer.WriteObject(testObject);
        writer.WriteObject(testObject);
        manager.OnFinishWrite(writer);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read = reader.ReadObjectLowLevel();

        Assert.NotNull(read);
        Assert.Equal(testObject, read);

        var read2 = reader.ReadObjectLowLevel();

        Assert.NotNull(read2);

        Assert.False(ReferenceEquals(testObject, read));
        Assert.True(ReferenceEquals(read, read2));
    }

    [Fact]
    public void ArchiveObject_UpdatableStruct()
    {
        var manager = new DefaultArchiveManager(false);
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, manager);
        var reader = new SArchiveMemoryReader(memoryStream, manager);

        var defaultStuff = default(TestObject3);

        var testObject = new TestObject3
        {
            Value1 = 1,
            Value2 = 2,
            Value3 = "hello, this is a much longer test string",
            Value4 = true,
        };

        Assert.NotEqual(defaultStuff.Value4, testObject.Value4);

        writer.WriteObjectProperties(ref testObject);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read = default(TestObject3);

        reader.ReadObjectProperties(ref read);

        Assert.NotEqual(testObject, read);

        Assert.Equal(testObject.Value1, read.Value1);
        Assert.Equal(testObject.Value2, read.Value2);
        Assert.Equal(testObject.Value3, read.Value3);
        Assert.Equal(defaultStuff.Value4, read.Value4);
        Assert.NotEqual(testObject.Value4, read.Value4);
    }

    private class TestObject1 : IArchivable
    {
        public const ushort SERIALIZATION_VERSION = 1;

        public float Value1;

        public int Value2;

        public string? Value3;

        public bool Value4;

        public ushort CurrentArchiveVersion => SERIALIZATION_VERSION;

        public ArchiveObjectType ArchiveObjectType => ArchiveObjectType.TestObjectType1;
        public virtual bool CanBeReferencedInArchive => false;

        public static bool operator ==(TestObject1? left, TestObject1? right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(TestObject1? left, TestObject1? right)
        {
            return !Equals(left, right);
        }

        public static TestObject1 ReadFromArchive(ISArchiveReader reader, ushort version)
        {
            if (version is > SERIALIZATION_VERSION or <= 0)
                throw new InvalidArchiveVersionException(version, SERIALIZATION_VERSION);

            var instance = new TestObject1
            {
                Value1 = reader.ReadFloat(),
                Value2 = reader.ReadInt32(),
                Value3 = reader.ReadString(),
                Value4 = reader.ReadBool(),
            };

            return instance;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Value1, Value2, Value3, Value4);
        }

        public void WriteToArchive(ISArchiveWriter writer)
        {
            writer.Write(Value1);
            writer.Write(Value2);
            writer.Write(Value3);
            writer.Write(Value4);
        }

        public override bool Equals(object? obj)
        {
            if (obj is null)
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            if (obj.GetType() != GetType())
                return false;

            return Equals((TestObject1)obj);
        }

        protected bool Equals(TestObject1 other)
        {
            return Value1.Equals(other.Value1) && Value2 == other.Value2 && Value3 == other.Value3 &&
                Value4 == other.Value4;
        }
    }

    private class TestObject2 : TestObject1
    {
        public override bool CanBeReferencedInArchive => true;

        // ReSharper disable once ArrangeModifiersOrder
        public static new TestObject2 ReadFromArchive(ISArchiveReader reader, ushort version)
        {
            if (version is > SERIALIZATION_VERSION or <= 0)
                throw new InvalidArchiveVersionException(version, SERIALIZATION_VERSION);

            var instance = new TestObject2
            {
                Value1 = reader.ReadFloat(),
                Value2 = reader.ReadInt32(),
                Value3 = reader.ReadString(),
                Value4 = reader.ReadBool(),
            };

            return instance;
        }
    }

    private struct TestObject3 : IArchiveUpdatable
    {
        public const ushort SERIALIZATION_VERSION = 1;

        public float Value1;
        public int Value2;
        public string? Value3;

        // Not saved
        public bool Value4;

        public ushort CurrentArchiveVersion => SERIALIZATION_VERSION;
        public ArchiveObjectType ArchiveObjectType => ArchiveObjectType.TestObjectType1;

        public void WritePropertiesToArchive(ISArchiveWriter writer)
        {
            writer.Write(Value1);
            writer.Write(Value2);
            writer.Write(Value3);

            // Intentionally, this shows off not writing the last value
        }

        public void ReadPropertiesFromArchive(ISArchiveReader reader, ushort version)
        {
            if (version is > SERIALIZATION_VERSION or <= 0)
                throw new InvalidArchiveVersionException(version, SERIALIZATION_VERSION);

            Value1 = reader.ReadFloat();
            Value2 = reader.ReadInt32();
            Value3 = reader.ReadString();
        }
    }
}
