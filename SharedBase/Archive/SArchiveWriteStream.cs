namespace SharedBase.Archive;

using System;
using System.IO;

/// <summary>
///   SArchive implementation that works for any <see cref="Stream"/> that is seekable (both for writing)
/// </summary>
public class SArchiveWriteStream : SArchiveWriterBase, IDisposable
{
    private readonly Stream stream;
    private readonly bool closeStream;

    public SArchiveWriteStream(Stream stream, IArchiveWriteManager writeManager, bool closeStream = true) :
        base(writeManager)
    {
        this.stream = stream;
        this.closeStream = closeStream;

        if (!stream.CanSeek)
            throw new ArgumentException("Stream must be seekable");
    }

    public override void Write(byte value)
    {
        stream.WriteByte(value);
    }

    public override void Write(ReadOnlySpan<byte> value)
    {
        stream.Write(value);
    }

    public override long GetPosition()
    {
        return stream.Position;
    }

    public override void Seek(long position)
    {
        stream.Seek(position, SeekOrigin.Begin);
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
