namespace SharedBase.Tests.Archive.Tests;

using System;
using System.IO;
using SharedBase.Archive;
using Xunit;

public class ArchiveObjectNestingTests
{
    [Fact]
    public void ArchiveObject_ReferringBackToParentObject()
    {
        var manager = new DefaultArchiveManager(false);
        manager.RegisterObjectType(ArchiveObjectType.TestObjectType1, typeof(TestObject1), TestObject1.ReadFromArchive);
        manager.RegisterObjectType(ArchiveObjectType.TestObjectType2, typeof(ChildObject), ChildObject.ReadFromArchive);
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, manager);
        var reader = new SArchiveMemoryReader(memoryStream, manager);

        var testObject = new TestObject1(1)
        {
            Value3 = true,
        };

        var child = new ChildObject(testObject, "test", 10);
        testObject.Value2 = child;

        manager.OnStartNewWrite(writer);
        writer.WriteObject(testObject);
        manager.OnFinishWrite(writer);

        memoryStream.Seek(0, SeekOrigin.Begin);

        manager.OnStartNewRead(reader);
        var read = reader.ReadObjectOrNull<TestObject1>();
        manager.OnFinishRead(reader);

        Assert.NotNull(read);
        Assert.Equal(testObject, read);

        Assert.NotNull(read.Value2);
        Assert.True(ReferenceEquals(read.Value2.Parent, read));

        // Make sure low-level read also works
        memoryStream.Seek(0, SeekOrigin.Begin);

        manager.OnStartNewRead(reader);
        var read2 = reader.ReadObjectLowLevel(out _);
        manager.OnFinishRead(reader);

        Assert.NotNull(read2);
        Assert.Equal(testObject, read2);
        Assert.Equal(read, read2);

        // As the objects cannot be marshalled, this is a manual count of bytes in them, don't add new fields!
        // Estimated header amounts
        var headers = 4;
        Assert.True(memoryStream.Length < 38 + headers);
    }

    private class TestObject1 : IArchivable
    {
        public const ushort SERIALIZATION_VERSION = 1;

        // Don't add new fields, as this is a manual count of bytes in them!
        public readonly int Value1;

        public ChildObject? Value2;

        public bool Value3;

        public TestObject1(int stuff)
        {
            Value1 = stuff;
        }

        public ushort CurrentArchiveVersion => SERIALIZATION_VERSION;
        public ArchiveObjectType ArchiveObjectType => ArchiveObjectType.TestObjectType1;

        public bool CanBeReferencedInArchive => true;

        public static TestObject1 ReadFromArchive(ISArchiveReader reader, ushort version, int referenceId)
        {
            if (version is > SERIALIZATION_VERSION or <= 0)
                throw new InvalidArchiveVersionException(version, SERIALIZATION_VERSION);

            var instance = new TestObject1(reader.ReadInt32());

            reader.ReportObjectConstructorDone(instance, referenceId);

            instance.Value2 = reader.ReadObjectOrNull<ChildObject>();
            instance.Value3 = reader.ReadBool();

            return instance;
        }

        public void WriteToArchive(ISArchiveWriter writer)
        {
            writer.Write(Value1);

            if (Value2 == null)
            {
                writer.WriteNullObject();
            }
            else
            {
                writer.WriteObject(Value2);
            }

            writer.Write(Value3);
        }

        public override bool Equals(object? obj)
        {
            if (obj is not TestObject1 other)
                return false;

            if (Value1 != other.Value1 || Value3 != other.Value3)
                return false;

            if (Value2 == null)
            {
                return other.Value2 == null;
            }

            return Value2.Equals(other.Value2);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Value1, Value3);
        }
    }

    private class ChildObject : IArchivable
    {
        public const ushort SERIALIZATION_VERSION = 1;

        // Don't add new fields, as this is a manual count of bytes in them!
        public string Name;
        public int Age;
        public TestObject1 Parent;

        public ChildObject(TestObject1 parent, string name, int age)
        {
            Name = name;
            Age = age;
            Parent = parent;
        }

        public ushort CurrentArchiveVersion => SERIALIZATION_VERSION;
        public ArchiveObjectType ArchiveObjectType => ArchiveObjectType.TestObjectType2;

        public bool CanBeReferencedInArchive => true;

        public static ChildObject ReadFromArchive(ISArchiveReader reader, ushort version, int referenceId)
        {
            if (version is > SERIALIZATION_VERSION or <= 0)
                throw new InvalidArchiveVersionException(version, SERIALIZATION_VERSION);

            var instance = new ChildObject(null!, null!, -1);
            reader.ReportObjectConstructorDone(instance, referenceId);

            instance.Parent = reader.ReadObjectOrNull<TestObject1>() ?? throw new NullArchiveObjectException();
            instance.Name = reader.ReadString() ?? throw new NullArchiveObjectException();
            instance.Age = reader.ReadInt32();
            return instance;
        }

        public void WriteToArchive(ISArchiveWriter writer)
        {
            writer.WriteObject(Parent);
            writer.Write(Name);
            writer.Write(Age);
        }

        public override bool Equals(object? obj)
        {
            if (obj is not ChildObject other)
                return false;

            // No good way to check the parent, so just check the name and age (as that causes a recursive call)
            return Name == other.Name && Age == other.Age;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Age, Parent.GetHashCode());
        }
    }
}
