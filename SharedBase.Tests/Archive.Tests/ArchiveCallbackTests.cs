namespace SharedBase.Tests.Archive.Tests;

using System;
using System.IO;
using SharedBase.Archive;
using Xunit;

public class ArchiveCallbackTests
{
    [Fact]
    public void ArchiveCallback_DelegateIsSerializedAndCallableAfterDeserialization()
    {
        var customManager = new DefaultArchiveManager(true);
        customManager.RegisterObjectType(ArchiveObjectType.TestObjectType1, typeof(CallableTestClass),
            CallableTestClass.WriteToArchive);
        customManager.RegisterObjectType(ArchiveObjectType.TestObjectType1, typeof(CallableTestClass),
            CallableTestClass.ReadFromArchive);

        var instance = new CallableTestClass(5155);
        CallableTestClass.TestDelegate testDelegate = instance.CallThatMethod;

        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, customManager);
        var reader = new SArchiveMemoryReader(memoryStream, customManager);

        writer.WriteDelegate(testDelegate);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read = reader.ReadDelegate<CallableTestClass.TestDelegate>();

        Assert.NotNull(read);

        Assert.Equal(instance.CallThatMethod(), read());
    }

    [Fact]
    public void ArchiveCallback_DelegateIsNotCreatedForAnyMethod()
    {
        var customManager = new DefaultArchiveManager(true);
        customManager.RegisterObjectType(ArchiveObjectType.TestObjectType1, typeof(CallableTestClass),
            CallableTestClass.WriteToArchive);
        customManager.RegisterObjectType(ArchiveObjectType.TestObjectType1, typeof(CallableTestClass),
            CallableTestClass.ReadFromArchive);

        var instance = new CallableTestClass(5155);
        CallableTestClass.TestDelegate testDelegate = instance.NotAllowedMethod;

        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, customManager);
        var reader = new SArchiveMemoryReader(memoryStream, customManager);

        writer.WriteDelegate(testDelegate, true);

        memoryStream.Seek(0, SeekOrigin.Begin);

        Assert.Throws<FormatException>(() => reader.ReadDelegate<CallableTestClass.TestDelegate>());
    }

    [Fact]
    public void ArchiveCallback_DelegateTargetIsKept()
    {
        var customManager = new DefaultArchiveManager(true);
        customManager.RegisterObjectType(ArchiveObjectType.TestObjectType1, typeof(CallableTestClass),
            CallableTestClass.WriteToArchive);
        customManager.RegisterObjectType(ArchiveObjectType.TestObjectType1, typeof(CallableTestClass),
            CallableTestClass.ReadFromArchive);

        var instance = new CallableTestClass(121);
        CallableTestClass.TestDelegate testDelegate = instance.CallThatMethod;

        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, customManager);
        var reader = new SArchiveMemoryReader(memoryStream, customManager);

        // Writing delegate first
        customManager.OnStartNewWrite(writer);
        writer.WriteDelegate(testDelegate);
        writer.WriteObject(instance);
        customManager.OnFinishWrite(writer);

        memoryStream.Seek(0, SeekOrigin.Begin);

        customManager.OnStartNewRead(reader);
        var readDelegate = reader.ReadDelegate<CallableTestClass.TestDelegate>();

        Assert.NotNull(readDelegate);

        var readObject = reader.ReadObjectOrNull<CallableTestClass>();
        Assert.NotNull(readObject);
        customManager.OnFinishRead(reader);

        Assert.Equal(readObject.CallThatMethod(), readDelegate());
        Assert.Same(readObject, readDelegate.Target);
        Assert.Equal(instance.ReturnedValue, readObject.ReturnedValue);

        // And writing object first
        memoryStream.Seek(0, SeekOrigin.Begin);

        customManager.OnStartNewWrite(writer);
        writer.WriteObject(instance);
        writer.WriteDelegate(testDelegate);
        customManager.OnFinishWrite(writer);

        memoryStream.Seek(0, SeekOrigin.Begin);

        customManager.OnStartNewRead(reader);
        readObject = reader.ReadObjectOrNull<CallableTestClass>();
        Assert.NotNull(readObject);

        readDelegate = reader.ReadDelegate<CallableTestClass.TestDelegate>();
        Assert.NotNull(readDelegate);
        customManager.OnFinishRead(reader);

        Assert.Equal(readObject.CallThatMethod(), readDelegate());
        Assert.Same(readObject, readDelegate.Target);
        Assert.Equal(instance.ReturnedValue, readObject.ReturnedValue);
    }

    [Fact]
    public void ArchiveCallback_DelegateIsNotCreatedForNonCallableMethod()
    {
        var customManager = new DefaultArchiveManager(true);
        customManager.RegisterObjectType(ArchiveObjectType.TestObjectType1, typeof(NonCallableTestClass),
            NonCallableTestClass.WriteToArchive);
        customManager.RegisterObjectType(ArchiveObjectType.TestObjectType1, typeof(NonCallableTestClass),
            NonCallableTestClass.ReadFromArchive);

        var instance = new NonCallableTestClass();
        CallableTestClass.TestDelegate testDelegate = instance.CallThatMethod;

        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, customManager);
        var reader = new SArchiveMemoryReader(memoryStream, customManager);

        Assert.ThrowsAny<Exception>(() =>
        {
            writer.WriteDelegate(testDelegate);

            memoryStream.Seek(0, SeekOrigin.Begin);

            Assert.Throws<FormatException>(() => reader.ReadDelegate<CallableTestClass.TestDelegate>());
        });
    }

    [Fact]
    public void ArchiveCallback_MisconfiguredClassThrows()
    {
        var customManager = new DefaultArchiveManager(true);
        customManager.RegisterObjectType(ArchiveObjectType.TestObjectType1, typeof(MisconfiguredClass),
            MisconfiguredClass.WriteToArchive);
        customManager.RegisterObjectType(ArchiveObjectType.TestObjectType1, typeof(MisconfiguredClass),
            MisconfiguredClass.ReadFromArchive);

        var instance = new MisconfiguredClass();
        CallableTestClass.TestDelegate testDelegate = instance.CallThatMethod;

        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, customManager);

        Assert.Throws<ArgumentException>(() => { writer.WriteDelegate(testDelegate); });
    }

    [Fact]
    public void ArchiveCallback_CallingStaticMethodWorks()
    {
        var customManager = new DefaultArchiveManager(true);

        // These are needed for type registration
        customManager.RegisterObjectType(ArchiveObjectType.TestObjectType1, typeof(CallableTestClass),
            CallableTestClass.WriteToArchive);
        customManager.RegisterObjectType(ArchiveObjectType.TestObjectType1, typeof(CallableTestClass),
            CallableTestClass.ReadFromArchive);

        CallableTestClass.TestDelegate testDelegate = CallableTestClass.GetStaticValue;

        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, customManager);
        var reader = new SArchiveMemoryReader(memoryStream, customManager);

        writer.WriteDelegate(testDelegate);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var read = reader.ReadDelegate<CallableTestClass.TestDelegate>();

        Assert.NotNull(read);

        Assert.Equal(CallableTestClass.GetStaticValue(), read());
    }

    public class CallableTestClass(int value) : IArchivable
    {
        public const ushort SERIALIZATION_VERSION = 1;

        public readonly int ReturnedValue = value;

        public delegate int TestDelegate();

        public ushort CurrentArchiveVersion => SERIALIZATION_VERSION;
        public ArchiveObjectType ArchiveObjectType => ArchiveObjectType.TestObjectType1;
        public bool CanBeReferencedInArchive => true;

        [ArchiveAllowedMethod]
        public static int GetStaticValue()
        {
            return 123;
        }

        public static void WriteToArchive(ISArchiveWriter writer, ArchiveObjectType type, object obj)
        {
            if (type != ArchiveObjectType.TestObjectType1)
                throw new NotSupportedException();

            ((CallableTestClass)obj).WriteToArchive(writer);
        }

        public static CallableTestClass ReadFromArchive(ISArchiveReader reader, ushort version)
        {
            if (version is > SERIALIZATION_VERSION or <= 0)
                throw new InvalidArchiveVersionException(version, SERIALIZATION_VERSION);

            return new CallableTestClass(reader.ReadInt32());
        }

        public void WriteToArchive(ISArchiveWriter writer)
        {
            writer.Write(ReturnedValue);
        }

        [ArchiveAllowedMethod]
        public int CallThatMethod()
        {
            return ReturnedValue;
        }

        public int NotAllowedMethod()
        {
            return -1;
        }
    }

    public class NonCallableTestClass : IArchivable
    {
        public const ushort SERIALIZATION_VERSION = 1;

        public ushort CurrentArchiveVersion => SERIALIZATION_VERSION;
        public ArchiveObjectType ArchiveObjectType => ArchiveObjectType.TestObjectType1;
        public bool CanBeReferencedInArchive => true;

        public static void WriteToArchive(ISArchiveWriter writer, ArchiveObjectType type, object obj)
        {
            if (type != ArchiveObjectType.TestObjectType1)
                throw new NotSupportedException();

            ((NonCallableTestClass)obj).WriteToArchive(writer);
        }

        public static NonCallableTestClass ReadFromArchive(ISArchiveReader reader, ushort version)
        {
            if (version is > SERIALIZATION_VERSION or <= 0)
                throw new InvalidArchiveVersionException(version, SERIALIZATION_VERSION);

            return new NonCallableTestClass();
        }

        public void WriteToArchive(ISArchiveWriter writer)
        {
        }

        public int CallThatMethod()
        {
            return -1;
        }
    }

    public class MisconfiguredClass : IArchivable
    {
        public const ushort SERIALIZATION_VERSION = 1;

        public ushort CurrentArchiveVersion => SERIALIZATION_VERSION;
        public ArchiveObjectType ArchiveObjectType => ArchiveObjectType.TestObjectType1;
        public bool CanBeReferencedInArchive => false;

        public static void WriteToArchive(ISArchiveWriter writer, ArchiveObjectType type, object obj)
        {
            if (type != ArchiveObjectType.TestObjectType1)
                throw new NotSupportedException();

            ((MisconfiguredClass)obj).WriteToArchive(writer);
        }

        public static MisconfiguredClass ReadFromArchive(ISArchiveReader reader, ushort version)
        {
            if (version is > SERIALIZATION_VERSION or <= 0)
                throw new InvalidArchiveVersionException(version, SERIALIZATION_VERSION);

            return new MisconfiguredClass();
        }

        public void WriteToArchive(ISArchiveWriter writer)
        {
        }

        [ArchiveAllowedMethod]
        public int CallThatMethod()
        {
            return -2;
        }
    }
}
