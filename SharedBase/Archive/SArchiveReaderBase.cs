namespace SharedBase.Archive;

using System;
using System.Buffers;
using System.Collections.Generic;
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

    public void ReadObjectHeader(out ArchiveObjectType type, out int referenceId, out bool isNull, out ushort version)
    {
        // Read the header and decode the bits
        var rawData = ReadUInt32();

        type = (ArchiveObjectType)(rawData >> 8);

        var versionRaw = (rawData >> 4) & 0xF;
        var versionIsLong = (versionRaw & 0x8) != 0;

        version = (ushort)(versionRaw & 0x7);

        bool canBeReference = (rawData & 0x1) != 0;
        isNull = (rawData & 0x2) != 0;

        // Read the extra fields if present
        if (!isNull)
        {
            if (versionIsLong)
            {
                version = ReadUInt16();
            }

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
    }

    public T? ReadObject<T>()
    {
        // TODO: should this verify something more?
        var rawRead = ReadObjectLowLevel(out _);

        if (rawRead == null)
            return (T?)rawRead;

        // TODO: more complex type matching?

        return (T?)rawRead;
    }

    public object? ReadObject(out ArchiveObjectType type)
    {
        return ReadObjectLowLevel(out type);
    }

    public void ReadAnyStruct<T>(ref T receiver)
    {
        ReadObjectHeader(out var type, out var id, out var isNull, out var version);

        if (isNull)
        {
            // Read a null object
            throw new FormatException("Encountered null object when reading something that cannot be null");
        }

#if DEBUG
        if (id > 0)
        {
            throw new FormatException("Reading an archive object as a struct that has references marked for it");
        }
#endif

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
            receiver = (T)ReadManager.ReadObject(this, type, version);
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

    public void ReadObject<T>(ref T obj)
        where T : IArchiveReadableVariable
    {
        ReadObjectHeader(out var type, out var id, out var isNull, out var version);

        if (isNull)
        {
            throw new FormatException("Encountered null object when reading something that cannot be null");
        }

        // As this can be used with classes, we do support reference IDs
        if (id > 0)
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
        ReadObjectHeader(out var type, out var id, out var isNull, out var version);

        if (isNull)
        {
            // Theoretically, someone might want to optionally read properties of an object that may have been null
            // when saved, so we don't throw an error here
            return false;
        }

        if (id > 0)
            throw new FormatException("Cannot read properties of an object that was written as a reference");

        // TODO: support for derived class reading here?
        if (obj.ArchiveObjectType != type)
            throw new FormatException($"Cannot read properties of an object ({obj.ArchiveObjectType}) from {type}");

        obj.ReadPropertiesFromArchive(this, version);
        return true;
    }

    public Type? MapArchiveTypeToType(ArchiveObjectType type)
    {
        return ReadManager.MapArchiveTypeToType(type);
    }

    public object? ReadObjectLowLevel(out ArchiveObjectType archiveObjectType)
    {
        ReadObjectHeader(out archiveObjectType, out var id, out var isNull, out var version);

        if (isNull)
        {
            // Read a null object, no further data
            return null;
        }

        // Return an already read object if we have it
        if (id > 0 && ReadManager.TryGetAlreadyReadObject(id, out var alreadyReadObject))
            return alreadyReadObject;

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

                processingObjectIds.Push(id);
            }

            read = ReadManager.ReadObject(this, archiveObjectType, version);
        }

        if (id > 0)
        {
            // Remove the ID if it was not used by ReportObjectConstructorDone
            if (processingObjectIds != null && processingObjectIds.Peek() == id)
            {
                processingObjectIds.Pop();
            }

            // Need to remember the object
            if (!ReadManager.RememberObject(read, id))
                throw new FormatException($"Multiple objects with same ID: {id}");
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
