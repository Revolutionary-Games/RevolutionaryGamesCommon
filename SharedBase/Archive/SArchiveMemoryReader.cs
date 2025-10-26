namespace SharedBase.Archive;

using System;
using System.IO;
using System.Text;

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
        // Ensure the buffer is visible
        try
        {
            stream.GetBuffer();
        }
        catch (UnauthorizedAccessException)
        {
            // To solve this a specific MemoryStream constructor needs to be used that sets the internal buffer as
            // accessible to the world
            throw new ArgumentException("Stream must have accessible internal buffer for efficiency");
        }

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
        var length = ReadStringHeader(out var isNull, out var multipleChunks);

        if (isNull)
            return null;

        if (length < 1)
            return string.Empty;

        int count = 0;
        while (true)
        {
            if (multipleChunks)
            {
                length = ReadUInt16();

                if (length < 1)
                    break;
            }

            var position = stream.Position;
            if (position + length > stream.Length)
                throw new FormatException("Too long string (not enough data)");

            if (position > int.MaxValue)
                throw new InvalidOperationException("String starts too far into an archive");

            if (length > 2 << 16)
                throw new FormatException($"String length in one buffer would be too much: {length}");

            ReadOnlySpan<byte> span = stream.GetBuffer().AsSpan((int)position, length);
            stream.Position += length;

            if (!multipleChunks)
            {
                return ISArchiveWriter.Utf8NoSignature.GetString(span);
            }

            multiPartReader ??= new StringBuilder();
            multiPartReader.Append(ISArchiveWriter.Utf8NoSignature.GetString(span));

            ++count;
            if (count > 1000)
                throw new FormatException("Too many string chunks");
        }

        if (multiPartReader == null)
            return string.Empty;

        var result = multiPartReader.ToString();
        multiPartReader.Clear();
        return result;
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
