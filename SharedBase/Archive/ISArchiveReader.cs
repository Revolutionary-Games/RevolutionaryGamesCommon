namespace SharedBase.Archive;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

public interface ISArchiveReader
{
    // This probably doesn't need async at this point as these are always used from background threads or when the
    // blocking is kind of necessary anyway

    public byte ReadInt8();

    public ushort ReadUInt16();
    public uint ReadUInt32();
    public ulong ReadUInt64();

    public short ReadInt16();
    public int ReadInt32();
    public long ReadInt64();

    public uint ReadVariableLengthField32();

    public float ReadFloat();
    public double ReadDouble();

    public string ReadString();

    public byte[] ReadBytes(int length);

    public byte[] ReadBytes(ulong length);

    /// <summary>
    ///   Reads bytes into the buffer. This is the preferred variant of reading multiple bytes at a time.
    /// </summary>
    /// <param name="buffer">Result. Should have the desired size.</param>
    public void ReadBytes(Span<byte> buffer);
}
