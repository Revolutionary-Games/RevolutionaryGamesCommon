namespace SharedBase.Archive;

using System;
using System.IO;

/// <summary>
///   Memory-buffer-based archive for writing
/// </summary>
public class SArchiveMemoryWriter : SArchiveWriterBase, IDisposable
{
    private readonly MemoryStream stream;

    public SArchiveMemoryWriter(IArchiveWriteManager writeManager, int reserve = 0) : base(writeManager)
    {
        stream = new MemoryStream();

        if (reserve > 0)
            stream.Capacity = reserve;
    }

    public SArchiveMemoryWriter(MemoryStream stream, IArchiveWriteManager writeManager) : base(writeManager)
    {
        this.stream = stream;
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
