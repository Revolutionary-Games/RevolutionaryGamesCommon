namespace SharedBase.Archive;

using System;
using System.Runtime.CompilerServices;
using System.Text;

public interface ISArchiveWriter
{
    public static Encoding Utf8NoSignature = new UTF8Encoding(false, true);

    public void Write(byte value);

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
        foreach (var stringByte in Utf8NoSignature.GetBytes(value))
            Write(stringByte);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(byte[] value)
    {
        Write(value.AsSpan());
    }

    public void Write(ReadOnlySpan<byte> value);

    /// <summary>
    ///   Write a variable length field that is up to 5 bytes long. Only uses 1-5 bytes depending on the value size.
    /// </summary>
    /// <param name="value">Value to write</param>
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

    public long GetPosition();
    public void Seek(long position);
}
