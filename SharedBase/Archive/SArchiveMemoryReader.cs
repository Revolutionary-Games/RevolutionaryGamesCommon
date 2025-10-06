namespace SharedBase.Archive;

using System;
using System.IO;

/// <summary>
///   Memory-buffer-based archive for reading. This is separate from <see cref="SArchiveReadStream"/> as this has
///   some specific memory optimizations.
/// </summary>
public class SArchiveMemoryReader : SArchiveReaderBase, IDisposable
{
    private readonly MemoryStream stream;
    private readonly bool closeStream;

    public SArchiveMemoryReader(MemoryStream stream, IArchiveReadManager readManager, bool closeStream = true) :
        base(readManager)
    {
        this.stream = stream;
        this.closeStream = closeStream;
    }

    public override byte ReadInt8()
    {
        return (byte)stream.ReadByte();
    }

    public override void ReadBytes(Span<byte> buffer)
    {
        if (stream.Read(buffer) != buffer.Length)
            throw new EndOfStreamException();
    }

    public override string? ReadString()
    {
        var lengthRaw = ReadVariableLengthField32();

        if (lengthRaw == 0)
            return null;

        lengthRaw >>= 1;

        if (lengthRaw == 0)
            return string.Empty;

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

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (closeStream)
                stream.Dispose();
        }
    }
}
