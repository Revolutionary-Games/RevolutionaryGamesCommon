namespace SharedBase.Archive;

using System;
using System.Text;

public interface ISArchiveWriter
{
    public static readonly Encoding Utf8NoSignature = new UTF8Encoding(false, true);

    public void Write(byte value);

    public void Write(bool value)
    {
        Write((byte)(value ? 1 : 0));
    }

    public void Write(short value);
    public void Write(int value);
    public void Write(long value);

    public void Write(float value);
    public void Write(double value);

    public void Write(ushort value);
    public void Write(uint value);
    public void Write(ulong value);

    public void Write(string? value);

    public void Write(byte[] value);

    public void Write(ReadOnlySpan<byte> value);

    /// <summary>
    ///   Write a variable length field that is up to 5 bytes long. Only uses 1-5 bytes depending on the value size.
    ///   This can write negative values, however, they always use the full 5 bytes,
    ///   and casting back is done like this: <c>unchecked((int)result)</c>
    /// </summary>
    /// <param name="value">Value to write</param>
    public void WriteVariableLengthField32(uint value);

    /// <summary>
    ///   Writes an object header. Writes extra fields like the full version BUT DOES NOT WRITE THE REFERENCE or the object
    ///   data.
    /// </summary>
    /// <param name="type">Type of the object</param>
    /// <param name="canBeReference">True if the object uses archive object references</param>
    /// <param name="isNull">True if the object instance is null (this prevents full version writing)</param>
    /// <param name="version">Version of the object. Must be at least 1.</param>
    public void WriteObjectHeader(ArchiveObjectType type, bool canBeReference, bool isNull, ushort version);

    public void WriteObject(IArchivable obj);
    public void WriteNullObject();

    /// <summary>
    ///   Helper for writing the object header for a struct / value type
    /// </summary>
    public void WriteStructHeader(ArchiveObjectType type, ushort version);

    public long GetPosition();
    public void Seek(long position);
}
