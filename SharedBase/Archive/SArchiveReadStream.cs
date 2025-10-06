namespace SharedBase.Archive;

using System;
using System.IO;

/// <summary>
///   SArchive implementation that works for any <see cref="Stream"/> (doesn't need to be seekable)
/// </summary>
public class SArchiveReadStream : SArchiveReaderBase, IDisposable
{
    private readonly Stream stream;
    private readonly bool closeStream;

    public SArchiveReadStream(Stream stream, IArchiveReadManager readManager, bool closeStream = true) :
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

    public void Dispose()
    {
        Dispose(true);
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
