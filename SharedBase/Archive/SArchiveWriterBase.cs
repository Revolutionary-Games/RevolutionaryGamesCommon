namespace SharedBase.Archive;

using System;
using System.Runtime.CompilerServices;
using System.Text;

/// <summary>
///   Abstract base with common methods for archive writers
/// </summary>
public abstract class SArchiveWriterBase : ISArchiveWriter
{
    private const int BUFFER_SIZE = 1024;

    private Encoder? textEncoder;
    private byte[]? scratch;

    protected SArchiveWriterBase(IArchiveWriteManager writeManager)
    {
        WriteManager = writeManager;

#if DEBUG
        if (!BitConverter.IsLittleEndian)
        {
            throw new PlatformNotSupportedException("This library supports only little-endian");
        }
#endif
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

    public virtual void Write(string? value)
    {
        if (value == null)
        {
            WriteVariableLengthField32(0);
            return;
        }

        if ((ulong)value.Length > uint.MaxValue >> 1)
            throw new ArgumentException("String is too long");

        var converted = (uint)value.Length;

        // The first bit is reserved for marking null strings. So for non-null it must be 1.
        converted = (converted << 1) | 0x1;

        WriteVariableLengthField32(converted);

        if (value.Length < 1)
            return;

        textEncoder ??= ISArchiveWriter.Utf8NoSignature.GetEncoder();
        var encoder = textEncoder;

        scratch ??= new byte[BUFFER_SIZE];
        Span<byte> buffer = scratch;

        ReadOnlySpan<char> chars = value.AsSpan();

        // Encode the string in parts to avoid allocating a large buffer
        int charIndex = 0;
        while (charIndex < chars.Length)
        {
            encoder.Convert(chars.Slice(charIndex), buffer,
                false, out int charsUsed, out int bytesUsed, out _);

            Write(buffer[..bytesUsed]);

            charIndex += charsUsed;
        }

        // Flush any remaining data
        encoder.Convert(ReadOnlySpan<char>.Empty, buffer,
            true, out _, out int finalBytes, out _);

        if (finalBytes > 0)
            Write(buffer[..finalBytes]);
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

    public void WriteObjectHeader(ArchiveObjectType type, bool canBeReference, bool isNull, ushort version)
    {
        if (type > ArchiveObjectType.LastValidObjectType)
            throw new ArgumentException("Invalid object type (value too high)");

        if (version <= 0)
            throw new ArgumentException("Version must be at least 1");

        bool versionIsLong = version > 0x7;

        // The object type takes at most 24 bits, so we have 8 bits for flags at the start
        // If the version takes more than 4 bits, we write a separate field for it
        uint archiveValue = (uint)type << 8 | (uint)(canBeReference ? 0x1 : 0) | (uint)(isNull ? 0x2 : 0) |
            (uint)((version & 0x7) << 4) | (uint)(versionIsLong ? 0x80 : 0);

        Write(archiveValue);

        if (versionIsLong && !isNull)
        {
            Write(version);
        }
    }

    public void WriteObject(IArchivable obj)
    {
        bool canBeReference = obj.CanBeReferencedInArchive;
        WriteObjectHeader(obj.ArchiveObjectType, canBeReference, false, obj.CurrentArchiveVersion);

        // If the object can be a reference, we write a placeholder for it, if it stays all 0, then it was not actually
        // referenced
        if (canBeReference)
        {
            if (WriteManager.MarkStartOfReferenceObject(this, obj))
            {
                // Object was already written, so we don't need to write it again, and the manager already put the ID
                // here, so we don't even need to write that
                return;
            }

            // Otherwise put the placeholder here where we are at the position the WriteManager saved
            Write(0);
        }

        // Header handled, let the object handle saving its data
        obj.WriteToArchive(this);
    }

    public void WriteNullObject()
    {
        WriteObjectHeader(ArchiveObjectType.Null, false, true, 1);

        // Nulls do not have a reference placeholder, even if they can be otherwise references
    }

    public void WriteStructHeader(ArchiveObjectType type, ushort version)
    {
        WriteObjectHeader(type, false, false, version);
    }

    public abstract long GetPosition();
    public abstract void Seek(long position);
}
