namespace SharedBase.Archive;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

/// <summary>
///   Abstract base with common methods for archive readers
/// </summary>
public abstract class SArchiveReaderBase : ISArchiveReader
{
    private const int BUFFER_SIZE = 1024;

    private byte[]? scratch;

    private Stack<int>? processingObjectIds;

    protected SArchiveReaderBase(IArchiveReadManager readManager)
    {
        ReadManager = readManager;
    }

    public IArchiveReadManager ReadManager { get; protected set; }

    public abstract byte ReadInt8();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort ReadUInt16()
    {
        return (ushort)(ReadInt8() | ReadInt8() << 8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadUInt32()
    {
        return ReadUInt16() | (uint)ReadUInt16() << 16;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong ReadUInt64()
    {
        return ReadUInt32() | (ulong)ReadUInt32() << 32;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public short ReadInt16()
    {
        return (short)(ReadInt8() | ReadInt8() << 8);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ReadInt32()
    {
        return ReadUInt16() | ReadUInt16() << 16;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ReadInt64()
    {
        return ReadUInt32() | (long)ReadUInt32() << 32;
    }

    public uint ReadVariableLengthField32()
    {
        uint result = 0;
        int shift = 0;

        for (int i = 0; i < 5; i++, shift += 7)
        {
            var currentByte = ReadInt8();
            result |= (uint)(currentByte & 0x7F) << shift;

            if ((currentByte & 0x80) == 0)
                return result;

            if (i == 4)
                throw new FormatException("Too many bytes in variable length field");
        }

        throw new FormatException("Variable-length field was not terminated");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ReadFloat()
    {
        return BitConverter.Int32BitsToSingle(ReadInt32());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double ReadDouble()
    {
        return BitConverter.Int64BitsToDouble(ReadInt64());
    }

    public virtual string? ReadString()
    {
        // First the length of the string
        var length = ReadVariableLengthField32();

        // Check null marker first
        if (length == 0)
            return null;

        length >>= 1;

        if (length == 0)
            return string.Empty;

        var lengthAsInt = (int)length;

        if (lengthAsInt <= BUFFER_SIZE)
        {
            scratch ??= new byte[BUFFER_SIZE];

            ReadBytes(scratch.AsSpan(0, lengthAsInt));
            return ISArchiveWriter.Utf8NoSignature.GetString(scratch, 0, lengthAsInt);
        }

        var pool = ArrayPool<byte>.Shared;
        byte[] buffer = pool.Rent(lengthAsInt);
        try
        {
            ReadBytes(buffer.AsSpan(0, lengthAsInt));
            return ISArchiveWriter.Utf8NoSignature.GetString(buffer, 0, lengthAsInt);
        }
        finally
        {
            pool.Return(buffer);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] ReadBytes(int length)
    {
        if (length < 0)
            throw new ArgumentException("Length cannot be negative");

        return ReadBytes((ulong)length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] ReadBytes(ulong length, bool temporaryBuffer = false)
    {
        if (temporaryBuffer && length <= BUFFER_SIZE)
        {
            scratch ??= new byte[BUFFER_SIZE];
            ReadBytes(scratch.AsSpan(0, (int)length));
            return scratch;
        }

        var buffer = new byte[length];
        ReadBytes(buffer);
        return buffer;
    }

    public abstract void ReadBytes(Span<byte> buffer);

    public void ReadObjectHeader(out ArchiveObjectType type, out int referenceId, out bool isNull,
        out bool referencesEarlier, out bool extendedType, out ushort version)
    {
        // Read the header and decode the bits
        var rawData = ReadUInt32();

        type = (ArchiveObjectType)(rawData >> 8);

        var versionRaw = (rawData >> 4) & 0xF;
        var versionIsLong = (versionRaw & 0x8) != 0;

        version = (ushort)(versionRaw & 0x7);

        bool canBeReference = (rawData & 0x1) != 0;
        isNull = (rawData & 0x2) != 0;
        referencesEarlier = (rawData & 0x4) != 0;

        if (!canBeReference && referencesEarlier)
        {
            // If new code has been added recently, and you get here, then that's likely a misconfiguration
            throw new FormatException(
                "Object that cannot be a reference cannot be marked as referencing a previous instance");
        }

        // Read the extra fields if present
        if (!isNull)
        {
            if (versionIsLong)
            {
                version = ReadUInt16();
            }

            // If the version is 0, it is very likely a sign that the archive is corrupt, and we are reading the wrong
            // location
            if (version == 0)
                throw new FormatException("Version of object should never be 0 when data is present");
        }
        else
        {
            version = 0;
        }

        if (canBeReference && !isNull)
        {
            referenceId = ReadInt32();
        }
        else
        {
            referenceId = -1;
        }

        extendedType = type.IsExtendedType();
    }

    public void ReadExtendedObjectType(ArchiveObjectType baseType, Span<ArchiveObjectType> extendedStorage,
        out int readElements)
    {
        var length = ReadInt8();

        if (length <= 0)
            throw new FormatException("Extended type length cannot be less than 1");

        if (length > ISArchiveWriter.ReasonableMaxExtendedType)
            throw new FormatException($"Extended type length too long: {length}");

        // Each type requires 24 bits
        var readLength = length * 3;

        Span<byte> readBuffer = stackalloc byte[readLength];

        ReadBytes(readBuffer);

        readElements = length;

        if (extendedStorage.Length < length)
        {
            throw new ArgumentException(
                $"Extended type length ({length}) is larger than the provided buffer ({extendedStorage.Length})");
        }

        int byteIndex = 0;

        // Decode the bytes into the receiver
        for (int i = 0; i < length; ++i)
        {
            extendedStorage[i] = (ArchiveObjectType)(readBuffer[byteIndex++] | readBuffer[byteIndex++] << 8 |
                readBuffer[byteIndex++] << 16);
        }

        if (byteIndex != readLength)
            throw new Exception("Read unexpected number of bytes from buffer");
    }

    public T? ReadObjectOrNull<T>()
    {
        // TODO: should this verify something more?
        var rawRead = ReadObjectLowLevel(out _);

        if (rawRead == null)
            return (T?)rawRead;

        // TODO: more complex type matching?

        return (T?)rawRead;
    }

    public object? ReadObjectOrNull(out ArchiveObjectType type)
    {
        return ReadObjectLowLevel(out type);
    }

    public void ReadAnyStruct<T>(ref T receiver)
    {
        ReadObjectHeader(out var type, out var id, out var isNull, out var references, out var extended,
            out var version);

        if (isNull)
        {
            // Read a null object
            throw new FormatException("Encountered null object when reading something that cannot be null");
        }

#if DEBUG
        if (id > 0 || references)
        {
            throw new FormatException("Reading an archive object as a struct that has references marked for it");
        }
#endif

        // Read the extended type if present
        Span<ArchiveObjectType> extendedStorage =
            stackalloc ArchiveObjectType[ISArchiveWriter.ReasonableMaxExtendedType];
        int usedExtendedStorage = 0;

        if (extended)
        {
            ReadExtendedObjectType(type, extendedStorage, out usedExtendedStorage);
        }

        // Anything with added handling here should also be put into ReadObjectLowLevel
        switch (type)
        {
            case ArchiveObjectType.Invalid:
            case ArchiveObjectType.Null:
                throw new FormatException("Invalid object type in archive specified (for struct)");

            case ArchiveObjectType.Byte:
                if (version > 1)
                    throw new InvalidArchiveVersionException(version, 1);

                // TODO: could allow receivers of bigger size to be used here
                if (receiver is byte)
                {
                    Unsafe.As<T, byte>(ref receiver) = ReadInt8();
                    return;
                }

                throw new FormatException($"Cannot read {type} into receiver of type {receiver!.GetType()}");

            case ArchiveObjectType.Bool:
                if (version > 1)
                    throw new InvalidArchiveVersionException(version, 1);

                if (receiver is bool)
                {
                    Unsafe.As<T, bool>(ref receiver) = ReadInt8() != 0;
                    return;
                }

                throw new FormatException($"Cannot read {type} into receiver of type {receiver!.GetType()}");

            case ArchiveObjectType.Int16:
                if (version > 1)
                    throw new InvalidArchiveVersionException(version, 1);

                if (receiver is short)
                {
                    Unsafe.As<T, short>(ref receiver) = ReadInt16();
                    return;
                }

                throw new FormatException($"Cannot read {type} into receiver of type {receiver!.GetType()}");
            case ArchiveObjectType.Int32:
                if (version > 1)
                    throw new InvalidArchiveVersionException(version, 1);

                if (receiver is int)
                {
                    Unsafe.As<T, int>(ref receiver) = ReadInt32();
                    return;
                }

                throw new FormatException($"Cannot read {type} into receiver of type {receiver!.GetType()}");
            case ArchiveObjectType.Int64:
                if (version > 1)
                    throw new InvalidArchiveVersionException(version, 1);

                if (receiver is long)
                {
                    Unsafe.As<T, long>(ref receiver) = ReadInt64();
                    return;
                }

                throw new FormatException($"Cannot read {type} into receiver of type {receiver!.GetType()}");
            case ArchiveObjectType.UInt16:
                if (version > 1)
                    throw new InvalidArchiveVersionException(version, 1);

                if (receiver is ushort)
                {
                    Unsafe.As<T, ushort>(ref receiver) = ReadUInt16();
                    return;
                }

                throw new FormatException($"Cannot read {type} into receiver of type {receiver!.GetType()}");
            case ArchiveObjectType.UInt32:
                if (version > 1)
                    throw new InvalidArchiveVersionException(version, 1);

                if (receiver is uint)
                {
                    Unsafe.As<T, uint>(ref receiver) = ReadUInt32();
                    return;
                }

                throw new FormatException($"Cannot read {type} into receiver of type {receiver!.GetType()}");
            case ArchiveObjectType.UInt64:
                if (version > 1)
                    throw new InvalidArchiveVersionException(version, 1);

                if (receiver is ulong)
                {
                    Unsafe.As<T, ulong>(ref receiver) = ReadUInt64();
                    return;
                }

                throw new FormatException($"Cannot read {type} into receiver of type {receiver!.GetType()}");
            case ArchiveObjectType.Float:
                if (version > 1)
                    throw new InvalidArchiveVersionException(version, 1);

                if (receiver is float)
                {
                    Unsafe.As<T, float>(ref receiver) = ReadFloat();
                    return;
                }

                throw new FormatException($"Cannot read {type} into receiver of type {receiver!.GetType()}");
            case ArchiveObjectType.Double:
                if (version > 1)
                    throw new InvalidArchiveVersionException(version, 1);

                if (receiver is double)
                {
                    Unsafe.As<T, double>(ref receiver) = ReadDouble();
                    return;
                }

                throw new FormatException($"Cannot read {type} into receiver of type {receiver!.GetType()}");
            case ArchiveObjectType.VariableUint32:
                if (version > 1)
                    throw new InvalidArchiveVersionException(version, 1);

                if (receiver is uint)
                {
                    Unsafe.As<T, uint>(ref receiver) = ReadVariableLengthField32();
                    return;
                }

                throw new FormatException($"Cannot read {type} into receiver of type {receiver!.GetType()}");
            case ArchiveObjectType.Tuple:
                // This is highly not recommended when tuples are known to be used as this causes boxing
                try
                {
                    receiver = (T)ReadTupleBoxed(version);
                    return;
                }
                catch (Exception e)
                {
                    throw new FormatException($"Cannot read {type} into receiver of type {receiver!.GetType()}", e);
                }
        }

        // Try a manager read for custom registered structs
        try
        {
            receiver = (T)ReadManager.ReadObject(this, type, extendedStorage.Slice(0, usedExtendedStorage), version);
        }
        catch (InvalidCastException e)
        {
            throw new FormatException($"Cannot read {type} into receiver of type {receiver!.GetType()}", e);
        }
    }

    public void ReadTuple<T1>(ref ValueTuple<T1> receiver)
    {
        // Read the item count
        var count = ReadInt8();

        // And forward the request, let other code handle this headache
        ArchiveBuiltInReaders.ReadValueTuple(ref receiver, count, this);
    }

    public void ReadTuple<T1, T2>(ref (T1 Item1, T2 Item2) receiver)
    {
        var count = ReadInt8();
        ArchiveBuiltInReaders.ReadValueTuple(ref receiver, count, this);
    }

    public void ReadTuple<T1, T2, T3>(ref (T1 Item1, T2 Item2, T3 Item3) receiver)
    {
        var count = ReadInt8();
        ArchiveBuiltInReaders.ReadValueTuple(ref receiver, count, this);
    }

    public void ReadTuple<T1, T2, T3, T4>(ref (T1 Item1, T2 Item2, T3 Item3, T4 Item4) receiver)
    {
        var count = ReadInt8();
        ArchiveBuiltInReaders.ReadValueTuple(ref receiver, count, this);
    }

    public object ReadTupleBoxed(ushort version)
    {
        return ArchiveBuiltInReaders.ReadValueTupleBoxed(this, version);
    }

    public void ReadArchiveHeader(out int overallVersion, out string programIdentifier, out string programVersion)
    {
        // Either file is very corrupt or someone managed to create an archive on a big endian system with flipped
        // bytes
        if (ReadUInt32() != ISArchiveWriter.Magic)
            throw new FormatException("Invalid magic bytes! This is not a valid archive");

        overallVersion = ReadInt32();

        if (overallVersion <= 0)
            throw new FormatException("Archive version cannot be less than 1");

        if (overallVersion > ISArchiveWriter.ArchiveHeaderVersion)
            throw new FormatException($"Archive version ({overallVersion}) is too new for this reader");

        // Read unused flag bytes
        ReadInt8();

        // Read the expected ending location of the header
        var expectedHeaderEnd = ReadUInt32();

        // Then the program identifier and version
        var identifier = ReadString();

        if (string.IsNullOrWhiteSpace(identifier) || identifier.Length > 1024)
            throw new FormatException("Read invalid program identifier from archive header");

        var version = ReadString();

        if (string.IsNullOrWhiteSpace(version) || version.Length > 1024)
            throw new FormatException("Read invalid program version from archive header");

        programIdentifier = identifier;
        programVersion = version;

        // Finally ending bytes of the header
        ReadInt8();
        if (ReadInt8() != 42)
            throw new FormatException("Didn't see expected archive header end");

        // If we had an API for checking the read position, we could check that here
        // if (GetPosition() != expectedHeaderEnd)
        _ = expectedHeaderEnd;
    }

    public void ReadArchiveFooter()
    {
        if (ReadInt8() != 42)
            throw new FormatException("Invalid archive footer (archive is corrupt or reading encountered a bug)");

        if (ReadInt8() != 255)
            throw new FormatException("Invalid archive footer (archive is corrupt or reading encountered a bug)");

        if (ReadInt8() != 42)
            throw new FormatException("Invalid archive footer (archive is corrupt or reading encountered a bug)");
    }

    public void ReadObjectOrNull<T>(ref T obj)
        where T : IArchiveReadableVariable
    {
        ReadObjectHeader(out var type, out var id, out var isNull, out var references, out var extended,
            out var version);

        if (isNull)
        {
            throw new FormatException("Encountered null object when reading something that cannot be null");
        }

        // As this can be used with classes, we do support reference IDs
        if (id > 0 || references)
        {
            // If T is struct here, this will cause boxing
#if DEBUG
            if (typeof(T).IsValueType)
            {
                throw new Exception(
                    "Causing boxing with incorrect archive reader usage (struct is marked as allowing references)");
            }
#endif

            if (ReadManager.TryGetAlreadyReadObject(id, out var alreadyReadObject))
            {
                obj = (T)alreadyReadObject;
                return;
            }

            if (references)
            {
                throw new FormatException("Object was marked as referencing something earlier in the archive. " +
                    "But it was not found. This is either corruption of the file or a bug in the saving code");
            }
        }

        Span<ArchiveObjectType> extendedStorage =
            stackalloc ArchiveObjectType[ISArchiveWriter.ReasonableMaxExtendedType];

        if (extended)
        {
            ReadExtendedObjectType(type, extendedStorage, out var usedExtendedStorage);

            if (usedExtendedStorage > 0)
            {
                // We could in theory use the extended information here, but we already have a target object.
                // So instead we just ignore the information to support changing the target type of the object /
                // the used read method; that should be safe.

                // We could also throw an exception here, but that wouldn't allow changing the reading code without
                // forcing an archive version upgrade.
                _ = usedExtendedStorage;
            }
        }

        ReadManager.ReadObjectToVariable(ref obj, this, type, version);

        if (id > 0)
        {
            // Need to remember the object
            if (!ReadManager.RememberObject(obj, id))
                throw new FormatException($"Multiple objects with same ID: {id}");
        }
    }

    public bool ReadObjectProperties<T>(ref T obj)
        where T : IArchiveUpdatable
    {
        ReadObjectHeader(out var type, out var id, out var isNull, out var references, out var extended,
            out var version);

        if (isNull)
        {
            // Theoretically, someone might want to optionally read properties of an object that may have been null
            // when saved, so we don't throw an error here
            return false;
        }

        if (id > 0 || references)
            throw new FormatException("Cannot read properties of an object that was written as a reference");

        // TODO: support for derived class reading here?
        if (obj.ArchiveObjectType != type)
            throw new FormatException($"Cannot read properties of an object ({obj.ArchiveObjectType}) from {type}");

        if (extended)
        {
            // ReadExtendedObjectType(type, extendedStorage, out var usedExtendedStorage);

            throw new FormatException(
                "Object we are reading properties into has extended type information, this would be totally ignored, " +
                "instead we throw this exception");
        }

        obj.ReadPropertiesFromArchive(this, version);
        return true;
    }

    public bool ReadObjectProperties<T>(T obj)
        where T : class, IArchiveUpdatable
    {
        ReadObjectHeader(out var type, out var id, out var isNull, out var references, out var extended,
            out var version);

        if (isNull)
            return false;

        if (id > 0 || references)
            throw new FormatException("Cannot read properties of an object that was written as a reference");

        // TODO: support for derived class reading here?
        if (obj.ArchiveObjectType != type)
            throw new FormatException($"Cannot read properties of an object ({obj.ArchiveObjectType}) from {type}");

        if (extended)
        {
            throw new FormatException(
                "Object we are reading properties into has extended type information, this would be totally ignored, " +
                "instead we throw this exception");
        }

        obj.ReadPropertiesFromArchive(this, version);
        return true;
    }

    public T? ReadDelegate<T>()
        where T : Delegate
    {
        ReadObjectHeader(out var type, out _, out var isNull, out var references, out var extended,
            out var version);

        if (isNull)
            return null;

        if (type != ArchiveObjectType.Delegate)
            throw new FormatException($"Expected a delegate at this point in archive, but got: {type}");

        if (references || extended)
        {
            throw new FormatException(
                "Cannot read delegate that was written as a reference or has extended type information");
        }

        if (version is > SArchiveWriterBase.DELEGATE_VERSION or <= 0)
            throw new InvalidArchiveVersionException(version, SArchiveWriterBase.DELEGATE_VERSION);

        var isStatic = ReadInt8() != 0;

        var target = ReadObjectOrNull(out _);

        if (target == null != isStatic)
            throw new FormatException("Delegate is not static and target is null (or vice-versa) misconfiguration");

        var methodName = ReadString();
        if (string.IsNullOrWhiteSpace(methodName))
            throw new FormatException("Delegate method name is empty");

        var classTypeRaw = (ArchiveObjectType)ReadUInt32();

        var classType = MapArchiveTypeToType(classTypeRaw);

        if (classType == null)
        {
            throw new FormatException(
                $"Cannot map archive type {classTypeRaw} to a type for a delegate (target method: {methodName})");
        }

        var flags = BindingFlags.Public | BindingFlags.NonPublic;

        if (isStatic)
        {
            flags |= BindingFlags.Static;
        }
        else
        {
            flags |= BindingFlags.Instance;
        }

        var method = classType.GetMethod(methodName, flags);

        if (method == null)
            throw new FormatException($"Cannot find method {methodName} on type {classType} (for delegate)");

        return (T)Delegate.CreateDelegate(typeof(T), target, method);
    }

    public Type? MapArchiveTypeToType(ArchiveObjectType type)
    {
        return ReadManager.MapArchiveTypeToType(type);
    }

    public object? ReadObjectLowLevel(out ArchiveObjectType archiveObjectType)
    {
        ReadObjectHeader(out archiveObjectType, out var id, out var isNull, out var references, out var extended,
            out var version);

        if (isNull)
        {
            // Read a null object, no further data
            return null;
        }

        // Return an already read object if we have it
        if (references)
        {
            if (id < 1)
            {
                throw new FormatException(
                    "Reference ID cannot be less than 1, but object marked as referencing something");
            }

            if (ReadManager.TryGetAlreadyReadObject(id, out var requiredObject))
                return requiredObject;

            // Hitting this exception likely means that an object is trying to deserialize itself from a reference
            // before it has registered with the ReadManager.
            // This happens when an object contains an object that directly or indirectly references it
            // (so back up the tree).
            // To support that, the object needs to construct itself with minimal parameters and
            // call ReportObjectConstructorDone before deserializing any child objects that might reference it.
            throw new AncestorReferenceException(id, archiveObjectType);
        }

        if (id > 0 && ReadManager.TryGetAlreadyReadObject(id, out var alreadyReadObject))
        {
            return alreadyReadObject;
        }

        Span<ArchiveObjectType> extendedStorage =
            stackalloc ArchiveObjectType[ISArchiveWriter.ReasonableMaxExtendedType];
        int usedExtendedStorage = 0;

        if (extended)
        {
            ReadExtendedObjectType(archiveObjectType, extendedStorage, out usedExtendedStorage);
        }

        object? read = null;

        // Handle some builtin struct types that will cause MAJOR BOXING (memory allocations)
        // Any values added here must also be updated in ReadAnyStruct
        switch (archiveObjectType)
        {
            case ArchiveObjectType.Invalid:
            case ArchiveObjectType.Null:
                throw new FormatException("Invalid object type in archive specified");

            case ArchiveObjectType.Byte:
                if (version > 1)
                    throw new InvalidArchiveVersionException(version, 1);

                read = ReadInt8();
                break;
            case ArchiveObjectType.Bool:
                if (version > 1)
                    throw new InvalidArchiveVersionException(version, 1);

                read = ReadInt8() != 0;
                break;
            case ArchiveObjectType.Int16:
                if (version > 1)
                    throw new InvalidArchiveVersionException(version, 1);

                read = ReadInt16();
                break;
            case ArchiveObjectType.Int32:
                if (version > 1)
                    throw new InvalidArchiveVersionException(version, 1);

                read = ReadInt32();
                break;
            case ArchiveObjectType.Int64:
                if (version > 1)
                    throw new InvalidArchiveVersionException(version, 1);

                read = ReadInt64();
                break;
            case ArchiveObjectType.UInt16:
                if (version > 1)
                    throw new InvalidArchiveVersionException(version, 1);

                read = ReadUInt16();
                break;
            case ArchiveObjectType.UInt32:
                if (version > 1)
                    throw new InvalidArchiveVersionException(version, 1);

                read = ReadUInt32();
                break;
            case ArchiveObjectType.UInt64:
                if (version > 1)
                    throw new InvalidArchiveVersionException(version, 1);

                read = ReadUInt64();
                break;
            case ArchiveObjectType.Float:
                if (version > 1)
                    throw new InvalidArchiveVersionException(version, 1);

                read = ReadFloat();
                break;
            case ArchiveObjectType.Double:
                if (version > 1)
                    throw new InvalidArchiveVersionException(version, 1);

                read = ReadDouble();
                break;
            case ArchiveObjectType.String:
                if (version > 1)
                    throw new InvalidArchiveVersionException(version, 1);

                read = ReadString();
                break;
            case ArchiveObjectType.VariableUint32:
                if (version > 1)
                    throw new InvalidArchiveVersionException(version, 1);

                read = ReadVariableLengthField32();
                break;

            // Reference tuple is handled by ReadManager

            case ArchiveObjectType.Tuple:
                read = ReadTupleBoxed(version);
                break;
        }

        // If we don't have an object yet, we need to deserialize one.
        // For this we must rely on the read manager's mapping.
        if (read == null)
        {
            if (id > 0)
            {
                processingObjectIds ??= new Stack<int>();

                if (processingObjectIds is { Count: > 0 } && processingObjectIds.Peek() == id)
                {
                    throw new FormatException($"Object with ID {id} is already on the archive read stack. " +
                        $"This means that there is an incorrectly configured ancestor reference from an object " +
                        $"up the tree to type {archiveObjectType}, " +
                        $"likely missing call to ReportObjectConstructorDone.");
                }

                processingObjectIds.Push(id);
            }

            try
            {
                read = ReadManager.ReadObject(this, archiveObjectType, extendedStorage.Slice(0, usedExtendedStorage),
                    version);
            }
            catch (Exception)
            {
                // Try to exit in a bit cleaner state even if this errored out
                // Clear this in case it is necessary later if someone tries again after the exception
                if (processingObjectIds is { Count: > 0 })
                    processingObjectIds.Pop();

                throw;
            }
        }

        if (id > 0)
        {
            // Remove the ID if it was not used by ReportObjectConstructorDone
            // Otherwise it is already remembered
            if (processingObjectIds is { Count: > 0 } && processingObjectIds.Peek() == id)
            {
                processingObjectIds.Pop();

                // Need to remember the object
                if (!ReadManager.RememberObject(read, id))
                    throw new FormatException($"Multiple objects with same ID: {id}");
            }
#if DEBUG
            else
            {
                if (!ReadManager.TryGetAlreadyReadObject(id, out var written) || !ReferenceEquals(written, read))
                {
                    throw new FormatException(
                        $"Another object has stolen the ID {id}, this is likely a bug in ancestor " +
                        $"reference configuration for: {read.GetType()}");
                }
#endif
            }
        }

        return read;
    }

    public void ReportObjectConstructorDone(object currentlyDeserializingObject)
    {
        // Current object reports its constructor is done
        if (processingObjectIds is { Count: > 0 })
        {
            var id = processingObjectIds.Pop();
            if (!ReadManager.RememberObject(currentlyDeserializingObject, id))
                throw new FormatException($"Multiple objects with same ID: {id} (direct report)");
        }
    }
}

public class AncestorReferenceException : FormatException
{
    public AncestorReferenceException(int referenceId, ArchiveObjectType archiveObjectType) : base(
        $"Cannot find earlier object with ID {referenceId} that is referenced by this object. " +
        $"Is this archive file corrupted? " +
        $"Or is the code for type {archiveObjectType} misconfigured related to ancestor serialization " +
        $"(maybe missing call to ReportObjectConstructorDone)?")
    {
    }
}
