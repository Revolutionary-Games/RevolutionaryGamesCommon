namespace SharedBase.Tests.Archive.Tests;

using System;
using System.IO;
using SharedBase.Archive;
using Xunit;

public class ArchiveErrorMessageTests
{
    [Fact]
    public void ArchiveObject_WronglyConfiguredAncestorReference()
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

        // This should cause an error message that points to the ancestor problem
        manager.OnStartNewRead(reader);
        var exception = Assert.Throws<AncestorReferenceException>(() => reader.ReadObject<TestObject1>());
        manager.OnFinishRead(reader);

        Assert.Contains(nameof(ArchiveObjectType.TestObjectType1), exception.Message);
        Assert.Contains(nameof(ISArchiveReader.ReportObjectConstructorDone), exception.Message);
        Assert.Contains("misconfigured related to ancestor", exception.Message);

        manager.OnStartNewRead(reader);
        memoryStream.Seek(0, SeekOrigin.Begin);

        // Low-level read should give the same exception
        var exception2 = Assert.Throws<AncestorReferenceException>(() => reader.ReadObjectLowLevel(out _));

        manager.OnFinishRead(reader);

        Assert.Equal(exception.Message, exception2.Message);
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

        public static TestObject1 ReadFromArchive(ISArchiveReader reader, ushort version)
        {
            if (version is > SERIALIZATION_VERSION or <= 0)
                throw new InvalidArchiveVersionException(version, SERIALIZATION_VERSION);

            var instance = new TestObject1(reader.ReadInt32());

            // Here's the bug, missing this:
            // reader.ReportObjectConstructorDone(instance);

            instance.Value2 = reader.ReadObject<ChildObject>();
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
    }

    private class ChildObject : IArchivable
    {
        public const ushort SERIALIZATION_VERSION = 1;

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

        public static ChildObject ReadFromArchive(ISArchiveReader reader, ushort version)
        {
            if (version is > SERIALIZATION_VERSION or <= 0)
                throw new InvalidArchiveVersionException(version, SERIALIZATION_VERSION);

            // These are separate lines to make it easier to debug
            var parent = reader.ReadObject<TestObject1>() ?? throw new NullArchiveObjectException();
            var name = reader.ReadString() ?? throw new NullArchiveObjectException();
            var age = reader.ReadInt32();

            return new ChildObject(parent, name, age);
        }

        public void WriteToArchive(ISArchiveWriter writer)
        {
            writer.WriteObject(Parent);
            writer.Write(Name);
            writer.Write(Age);
        }
    }
}
