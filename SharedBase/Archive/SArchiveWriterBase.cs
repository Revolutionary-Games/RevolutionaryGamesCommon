namespace SharedBase.Archive;

using System;
using System.Runtime.CompilerServices;

/// <summary>
///   Abstract base with common methods for archive writers
/// </summary>
public abstract class SArchiveWriterBase : ISArchiveWriter
{
    protected SArchiveWriterBase(IArchiveWriteManager writeManager)
    {
        WriteManager = writeManager;
    }

    public IArchiveWriteManager WriteManager { get; protected set; }

    public abstract void Write(byte value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(short value)
    {
        Write((byte)value);
        Write((byte)(value >> 8));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(int value)
    {
        Write((byte)value);
        Write((byte)(value >> 8));
        Write((byte)(value >> 16));
        Write((byte)(value >> 24));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(long value)
    {
        Write((byte)value);
        Write((byte)(value >> 8));
        Write((byte)(value >> 16));
        Write((byte)(value >> 24));
        Write((byte)(value >> 32));
        Write((byte)(value >> 40));
        Write((byte)(value >> 48));
        Write((byte)(value >> 56));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(float value)
    {
        Write(BitConverter.SingleToInt32Bits(value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(double value)
    {
        Write(BitConverter.DoubleToInt64Bits(value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(ushort value)
    {
        Write((byte)value);
        Write((byte)(value >> 8));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(uint value)
    {
        Write((byte)value);
        Write((byte)(value >> 8));
        Write((byte)(value >> 16));
        Write((byte)(value >> 24));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(ulong value)
    {
        Write((byte)value);
        Write((byte)(value >> 8));
        Write((byte)(value >> 16));
        Write((byte)(value >> 24));
        Write((byte)(value >> 32));
        Write((byte)(value >> 40));
        Write((byte)(value >> 48));
        Write((byte)(value >> 56));
    }

    public void Write(string value)
    {
        if ((ulong)value.Length > uint.MaxValue)
            throw new ArgumentException("String is too long");

        WriteVariableLengthField32((uint)value.Length);

        if (value.Length < 1)
            return;

        // Write the data
        foreach (var stringByte in ISArchiveWriter.Utf8NoSignature.GetBytes(value))
            Write(stringByte);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(byte[] value)
    {
        Write(value.AsSpan());
    }

    public abstract void Write(ReadOnlySpan<byte> value);

    public void WriteVariableLengthField32(uint value)
    {
        while (true)
        {
            // Stop when written all bytes
            if ((value & 0xFFFFFF80) == 0)
                break;

            // 7 bits with the continuation flag set
            var currentByte = (byte)(value & 0x7F | 0x80);
            Write(currentByte);

            value >>= 7;
        }

        // Write the last byte without the continuation flag
        Write((byte)value);
    }

    public void WriteObjectHeader(ArchiveObjectType type, bool canBeReference, ushort version)
    {
        if (type >= ArchiveObjectType.LastValidObjectType)
            throw new ArgumentException("Invalid object type (value too high)");

        if (version <= 0)
            throw new ArgumentException("Version must be at least 1");

        bool versionIsLong = version > 0x7;

        // The object type takes at most 24 bits, so we have 8 bits for flags at the start
        // If the version takes more than 4 bits, we write a separate field for it
        uint archiveValue = (uint)type << 24 | (uint)(canBeReference ? 0x1 : 0) | (uint)(version & 0x7 << 4) |
            (uint)(versionIsLong ? 0x8 : 0);

        if (versionIsLong)
        {
            Write(version);
        }
    }

    public void WriteObject(IArchivable obj, bool canBeReference)
    {
        WriteObjectHeader(obj.ArchiveObjectType, canBeReference, obj.CurrentArchiveVersion);

        // If the object can be a reference, we write a placeholder for it, if it stays all 0, then it was not actually
        // referenced
        if (canBeReference)
        {
            if (WriteManager.MarkStartOfReferenceObject(this, obj))
            {
                // Object was already written, so we don't need to write it again
                return;
            }

            // Otherwise put the placeholder here where we are at the position the WriteManager saved
            Write(0);
        }

        // Header handled, let the object handle saving its data
        obj.WriteToArchive(this);
    }

    public void WriteStructHeader(ArchiveObjectType type, ushort version)
    {
        WriteObjectHeader(type, false, version);
    }

    public abstract long GetPosition();
    public abstract void Seek(long position);
}
