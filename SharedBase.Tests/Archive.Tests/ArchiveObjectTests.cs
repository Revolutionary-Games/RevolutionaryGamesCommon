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

    private class TestObject1 : IArchivable
    {
        public float Value1;

        public int Value2;

        public string? Value3;

        public bool Value4;

        public ushort CurrentArchiveVersion => 1;

        public ArchiveObjectType ArchiveObjectType => ArchiveObjectType.TestObjectType1;
        public bool CanBeReferencedInArchive => false;

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
            if (version is > 1 or <= 0)
                throw new InvalidArchiveVersionException(version, 1);

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
}
