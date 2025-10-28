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

    [Fact]
    public void ArchiveCallback_ArchiveUpdatableObjectsCanHaveCallbacks()
    {
        var customManager = new DefaultArchiveManager(false);

        // These are needed for type registration
        customManager.RegisterObjectType(ArchiveObjectType.TestObjectType2,
            typeof(PropertyReadableReceiver.ChildWithCallBacks),
            PropertyReadableReceiver.ChildWithCallBacks.WriteToArchive);
        customManager.RegisterObjectType(ArchiveObjectType.TestObjectType2,
            typeof(PropertyReadableReceiver.ChildWithCallBacks),
            PropertyReadableReceiver.ChildWithCallBacks.ReadFromArchive);

        customManager.RegisterLimitedObjectType(ArchiveObjectType.TestObjectType1, typeof(PropertyReadableReceiver));

        var original = new PropertyReadableReceiver("text stuff")
        {
            OurValue = 1113,
        };

        Assert.NotNull(original.Child);
        original.Child.Value = 53;

        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, customManager);
        var reader = new SArchiveMemoryReader(memoryStream, customManager);

        customManager.OnStartNewWrite(writer);
        writer.WriteObjectProperties(original);
        customManager.OnFinishWrite(writer);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var readData = new PropertyReadableReceiver("stuff");

        Assert.NotEqual(original.Stuff, readData.Stuff);

        customManager.OnStartNewRead(reader);
        reader.ReadObjectProperties(readData);
        customManager.OnFinishRead(reader);

        Assert.Equal(original.OurValue, readData.OurValue);
        Assert.Equal(original.Stuff, readData.Stuff);
        Assert.NotNull(readData.Child);
        Assert.Equal(original.Child.Value, readData.Child.Value);

        Assert.Equal(original.Child.Call(), readData.Child.Call());

        Assert.Equal(original.Child.OnStuff(342453), readData.Child.OnStuff(342453));

        Assert.Same(readData, readData.Child.OnStuff.Target);

        var old = readData.Child.Call();

        readData.OurValue *= 2;

        Assert.NotEqual(old, readData.Child.Call());
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

            writer.WriteObject((CallableTestClass)obj);
        }

        public static CallableTestClass ReadFromArchive(ISArchiveReader reader, ushort version, int referenceId)
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

            writer.WriteObject((NonCallableTestClass)obj);
        }

        public static NonCallableTestClass ReadFromArchive(ISArchiveReader reader, ushort version, int referenceId)
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

            writer.WriteObject((MisconfiguredClass)obj);
        }

        public static object ReadFromArchive(ISArchiveReader reader, ushort version, int referenceId)
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

    public class PropertyReadableReceiver : IArchiveUpdatable
    {
        public const ushort SERIALIZATION_VERSION_OUTER = 1;

        public int OurValue;
        public string Stuff;

        public ChildWithCallBacks? Child;

        public PropertyReadableReceiver(string stuff)
        {
            Stuff = stuff;
            Child = new ChildWithCallBacks(DoStuff);
        }

        public delegate int OnStuffHappened(int value);

        public ushort CurrentArchiveVersion => SERIALIZATION_VERSION_OUTER;
        public ArchiveObjectType ArchiveObjectType => ArchiveObjectType.TestObjectType1;

        public bool CanBeSpecialReference => true;

        [ArchiveAllowedMethod]
        public int DoStuff(int value)
        {
            return value * OurValue;
        }

        public void WritePropertiesToArchive(ISArchiveWriter writer)
        {
            writer.Write(OurValue);
            writer.Write(Stuff);
            writer.WriteObjectOrNull(Child);
        }

        public void ReadPropertiesFromArchive(ISArchiveReader reader, ushort version)
        {
            OurValue = reader.ReadInt32();
            Stuff = reader.ReadString() ?? throw new NullArchiveObjectException();
            Child = reader.ReadObjectOrNull<ChildWithCallBacks>();
        }

        public class ChildWithCallBacks : IArchivable
        {
            public const ushort SERIALIZATION_VERSION_INNER = 1;

            public OnStuffHappened OnStuff;
            public int Value;

            public ChildWithCallBacks(OnStuffHappened onStuff)
            {
                OnStuff = onStuff;
                Value = 1;
            }

            public ushort CurrentArchiveVersion => SERIALIZATION_VERSION_INNER;
            public ArchiveObjectType ArchiveObjectType => ArchiveObjectType.TestObjectType2;
            public bool CanBeReferencedInArchive => false;

            public static void WriteToArchive(ISArchiveWriter writer, ArchiveObjectType type, object obj)
            {
                if (type != ArchiveObjectType.TestObjectType2)
                    throw new NotSupportedException();

                writer.WriteObject((ChildWithCallBacks)obj);
            }

            public static ChildWithCallBacks ReadFromArchive(ISArchiveReader reader, ushort version, int referenceId)
            {
                if (version is > SERIALIZATION_VERSION_INNER or <= 0)
                    throw new InvalidArchiveVersionException(version, SERIALIZATION_VERSION_INNER);

                return new ChildWithCallBacks(reader.ReadDelegate<OnStuffHappened>() ??
                    throw new NullArchiveObjectException())
                {
                    Value = reader.ReadInt32(),
                };
            }

            public void WriteToArchive(ISArchiveWriter writer)
            {
                writer.WriteDelegate(OnStuff);
                writer.Write(Value);
            }

            public int Call()
            {
                return OnStuff(Value);
            }
        }
    }
}
