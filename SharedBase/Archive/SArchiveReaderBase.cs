namespace SharedBase.Archive;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

/// <summary>
///   Abstract base with common methods for archive readers
/// </summary>
public abstract class SArchiveReaderBase : ISArchiveReader
{
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

    public virtual string ReadString()
    {
        // First the length of the string
        var length = ReadVariableLengthField32();

        if (length == 0)
            return string.Empty;

        if (length > int.MaxValue)
            throw new FormatException("Too long string");

        var lengthAsInt = (int)length;

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
    public byte[] ReadBytes(ulong length)
    {
        var buffer = new byte[length];
        ReadBytes(buffer);
        return buffer;
    }

    public abstract void ReadBytes(Span<byte> buffer);
}
