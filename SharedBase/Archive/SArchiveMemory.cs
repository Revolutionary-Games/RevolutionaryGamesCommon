namespace SharedBase.Archive;

using System;
using System.IO;

/// <summary>
///   Memory-buffer-based archive
/// </summary>
public class SArchiveMemory : ISArchiveReader, ISArchiveWriter, IDisposable
{
    private readonly MemoryStream stream;

    public SArchiveMemory(int reserveSize = 0)
    {
        stream = new MemoryStream();

        if (reserveSize > 0)
            stream.Capacity = reserveSize;
    }

    public SArchiveMemory(MemoryStream stream)
    {
        this.stream = stream;
    }

    public byte ReadInt8()
    {
        return (byte)stream.ReadByte();
    }

    public void ReadBytes(Span<byte> buffer)
    {
        if (stream.Read(buffer) != buffer.Length)
            throw new EndOfStreamException();
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

    public string ReadString()
    {
        var lengthRaw = ReadVariableLengthField32();

        if (lengthRaw == 0)
            return string.Empty;

        if (lengthRaw > int.MaxValue)
            throw new FormatException("Too long string");

        int length = (int)lengthRaw;

        var position = stream.Position;

        if (position + length > stream.Length)
            throw new FormatException("Too long string (not enough data)");

        if (position > int.MaxValue)
            throw new InvalidOperationException("String starts too far into an archive");

        ReadOnlySpan<byte> span = stream.GetBuffer().AsSpan((int)position, length);

        stream.Position += length;

        return ISArchiveWriter.Utf8NoSignature.GetString(span);
    }

    public void Write(byte value)
    {
        stream.WriteByte(value);
    }

    public void Write(ReadOnlySpan<byte> value)
    {
        stream.Write(value);
    }

    public long GetPosition()
    {
        return stream.Position;
    }

    public void Seek(long position)
    {
        if (stream.Length < position)
            throw new ArgumentException("Position is beyond the end of the stream");

        stream.Position = position;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            stream.Dispose();
        }
    }
}
