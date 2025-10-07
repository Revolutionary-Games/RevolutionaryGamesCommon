namespace SharedBase.Archive;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

/// <summary>
///   Abstract base with common methods for archive readers
/// </summary>
public abstract class SArchiveReaderBase : ISArchiveReader
{
    private const int BUFFER_SIZE = 1024;

    private byte[]? scratch;

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

    public object? ReadObjectLowLevel()
    {
        ReadObjectHeader(out var type, out var id, out var isNull, out var version);

        if (isNull)
        {
            // Read a null object, no further data
            return null;
        }

        // Return an already read object if we have it
        if (id > 0 && ReadManager.TryGetAlreadyReadObject(id, out var alreadyReadObject))
            return alreadyReadObject;

        // And if not, we need to deserialize an object. For this we must rely on the read manager's mapping.
        var read = ReadManager.ReadObject(this, type, version);

        if (id > 0)
        {
            // Need to remember the object
            if (!ReadManager.RememberObject(read, id))
                throw new FormatException($"Multiple objects with same ID: {id}");
        }

        return read;
    }
}
