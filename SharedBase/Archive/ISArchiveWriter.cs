namespace SharedBase.Archive;

using System;
using System.Runtime.CompilerServices;
using System.Text;

public interface ISArchiveWriter
{
    public static readonly Encoding Utf8NoSignature = new UTF8Encoding(false, true);

    public void Write(byte value);

    public void Write(short value);
    public void Write(int value);
    public void Write(long value);

    public void Write(float value);
    public void Write(double value);

    public void Write(ushort value);
    public void Write(uint value);
    public void Write(ulong value);

    public void Write(string value);

    public void Write(byte[] value);

    public void Write(ReadOnlySpan<byte> value);

    /// <summary>
    ///   Write a variable length field that is up to 5 bytes long. Only uses 1-5 bytes depending on the value size.
    /// </summary>
    /// <param name="value">Value to write</param>
    public void WriteVariableLengthField32(uint value);

    public void WriteObjectHeader(ArchiveObjectType type, bool canBeReference, ushort version);

    public void WriteObject(IArchivable obj, bool canBeReference);

    /// <summary>
    ///   Helper for writing the object header for a struct / value type
    /// </summary>
    public void WriteStructHeader(ArchiveObjectType type, ushort version);

    public long GetPosition();
    public void Seek(long position);
}
