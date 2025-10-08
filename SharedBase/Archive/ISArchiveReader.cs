namespace SharedBase.Archive;

using System;

public interface ISArchiveReader
{
    // This probably doesn't need async at this point as these are always used from background threads or when the
    // blocking is kind of necessary anyway

    public byte ReadInt8();

    public bool ReadBool()
    {
        return ReadInt8() != 0;
    }

    public ushort ReadUInt16();
    public uint ReadUInt32();
    public ulong ReadUInt64();

    public short ReadInt16();
    public int ReadInt32();
    public long ReadInt64();

    public uint ReadVariableLengthField32();

    public float ReadFloat();
    public double ReadDouble();

    /// <summary>
    ///   Reads a string from the archive.
    ///   Returns null if the string was originally null.
    ///   Returns an empty string if the original string was empty.
    /// </summary>
    /// <returns>String or null</returns>
    public string? ReadString();

    public byte[] ReadBytes(int length);

    /// <summary>
    ///   Reads a sequence of bytes as an array.
    /// </summary>
    /// <param name="length">Length to read</param>
    /// <param name="temporaryBuffer">
    ///   If set to true, then an optimised scratch buffer can be used for small lengths. Note that in this case the
    ///   returned buffer is only allowed to be used until the next archive call!
    /// </param>
    /// <returns>
    ///   Buffer containing the read bytes of the given length (will throw if not enough data is available)
    /// </returns>
    public byte[] ReadBytes(ulong length, bool temporaryBuffer = false);

    /// <summary>
    ///   Reads bytes into the buffer. This is the preferred variant of reading multiple bytes at a time.
    /// </summary>
    /// <param name="buffer">Result. Should have the desired size.</param>
    public void ReadBytes(Span<byte> buffer);

    /// <summary>
    ///   Reads the header of an object from the archive at the current position. Decodes the extra header parts like
    ///   reference ID and full version. For what the fields mean <see cref="ISArchiveWriter.WriteObjectHeader"/>.
    /// </summary>
    public void ReadObjectHeader(out ArchiveObjectType type, out int referenceId, out bool isNull,
        out bool referencesEarlier, out ushort version);

    /// <summary>
    ///   Reads an object from the archive. Must be called just after the header is read. Not suitable for structs.
    /// </summary>
    /// <typeparam name="T">
    ///   Type to read. Note that the object is read as normal first and then tries to be cast to the wanted type.
    /// </typeparam>
    /// <returns>Read object or null if there was a null marker instead</returns>
    public T? ReadObject<T>();

    /// <summary>
    ///   Pure raw read of any kind of object.
    /// </summary>
    /// <param name="type">Type of the returned object</param>
    /// <returns>Read object or if the object header specified null, then a null value</returns>
    public object? ReadObject(out ArchiveObjectType type);

    /// <summary>
    ///   Reads any struct type from the archive.
    ///   As long as it is one that is supported by the reader.
    ///   This variant exists to avoid boxing with the generic object read API.
    /// </summary>
    /// <param name="receiver">Variable that receives the struct value</param>
    /// <typeparam name="T">Type to read, needs to be either a built-in type or one that is registered</typeparam>
    /// <remarks>
    ///   <para>
    ///     This is the reading side equivalent of <see cref="ISArchiveWriter.WriteAnyRegisteredValueAsObject"/>.
    ///   </para>
    /// </remarks>
    public void ReadAnyStruct<T>(ref T receiver);

    /// <summary>
    ///   Reads an object / struct from the archive.
    /// </summary>
    /// <param name="obj">Where to place the read object</param>
    /// <typeparam name="T">Type of the object to read</typeparam>
    public void ReadObject<T>(ref T obj)
        where T : IArchiveReadableVariable;

    /// <summary>
    ///   Read properties of an object that was saved with <see cref="ISArchiveWriter.WriteObjectProperties{T}"/>
    /// </summary>
    /// <param name="obj">Object to update</param>
    /// <typeparam name="T">Type of the object</typeparam>
    /// <returns>True if properties were read</returns>
    public bool ReadObjectProperties<T>(ref T obj)
        where T : IArchiveUpdatable;

    public void ReadTuple<T1>(ref ValueTuple<T1> receiver);
    public void ReadTuple<T1, T2>(ref (T1 Item1, T2 Item2) receiver);
    public void ReadTuple<T1, T2, T3>(ref (T1 Item1, T2 Item2, T3 Item3) receiver);
    public void ReadTuple<T1, T2, T3, T4>(ref (T1 Item1, T2 Item2, T3 Item3, T4 Item4) receiver);

    public object ReadTupleBoxed(ushort version);

    /// <summary>
    ///   Used to support objects referring to their ancestors.
    ///   Absolutely do not call if the object is not marked as an allowed reference!
    /// </summary>
    public void ReportObjectConstructorDone(object currentlyDeserializingObject);

    public Type? MapArchiveTypeToType(ArchiveObjectType type);
}
