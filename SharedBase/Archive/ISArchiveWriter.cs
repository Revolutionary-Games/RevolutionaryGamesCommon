namespace SharedBase.Archive;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

public interface ISArchiveWriter
{
    public const int ReasonableMaxExtendedType = 64;

    /// <summary>
    ///   Overall version of the archive.
    ///   If this is updated, then archive headers will need much new handling and version difference compensation.
    ///   So do not change this lightly!
    /// </summary>
    public const int ArchiveHeaderVersion = 1;

    public const uint Magic = (byte)'T' | (byte)'a' << 8 | (byte)'r' << 16 | (byte)'c' << 24;

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
    ///   Writes an object header. Writes extra fields like the full version BUT DOES NOT WRITE THE REFERENCE or the
    ///   object data.
    /// </summary>
    /// <param name="type">Type of the object</param>
    /// <param name="canBeReference">True if the object uses archive object references</param>
    /// <param name="isNull">True if the object instance is null (this prevents full version writing)</param>
    /// <param name="usesExistingReference">
    ///     If true and <see cref="canBeReference"/> then marks that there should be a valid object reference back to
    ///     an earlier object after this header.
    ///     This is used for programming error detection when things are misconfigured.
    /// </param>
    /// <param name="extendedType">
    ///   Must be set to true when <see cref="type"/> is an extended type. The caller must then immediately afterwards
    ///   call <see cref="WriteExtendedObjectType"/>.
    /// </param>
    /// <param name="version">Version of the object. Must be at least 1.</param>
    public void WriteObjectHeader(ArchiveObjectType type, bool canBeReference, bool isNull, bool usesExistingReference,
        bool extendedType, ushort version);

    /// <summary>
    ///   Writes an extended object type
    /// </summary>
    /// <param name="baseType">Base type, which is not used for anything except double-checking</param>
    /// <param name="extendedTypes">
    ///     The extended types of the object,
    ///     as calculated by <see cref="IArchiveWriteManager.CalculateExtendedObjectType"/>
    /// </param>
    /// <param name="count">How many elements are valid in <see cref="extendedTypes"/></param>
    public void WriteExtendedObjectType(ArchiveObjectType baseType, Span<ArchiveObjectType> extendedTypes, int count);

    /// <summary>
    ///   Writes an object. This will write the object header and the object data. Don't use this on structs as this
    ///   will box them.
    /// </summary>
    /// <param name="obj">Object to write. If null use <see cref="WriteNullObject"/></param>
    public void WriteObject(IArchivable obj);

    /// <summary>
    ///   Writing variant that doesn't box value types like structs and cannot be a reference inside the
    ///   archive, but otherwise works the same as the above WriteObject.
    /// </summary>
    public void WriteObject<T>(ref T obj)
        where T : struct, IArchivable;

    /// <summary>
    ///   Writes the properties of an object rather than the entire object itself.
    /// </summary>
    /// <param name="obj">Object that can write its properties</param>
    /// <typeparam name="T">Type of the object to write</typeparam>
    public void WriteObjectProperties<T>(ref T obj)
        where T : IArchiveUpdatable;

    /// <summary>
    ///   Writes object properties of class-types
    /// </summary>
    public void WriteObjectProperties<T>(T obj)
        where T : class, IArchiveUpdatable;

    /// <summary>
    ///   Tries to write any type of value that is archivable or has a registered writer.
    ///   This is much less preferable to use than any of the more specific APIs available.
    ///   Throws if it cannot be processed.
    /// </summary>
    /// <param name="value">Value to write to this archive. This is always written with an object header.</param>
    /// <typeparam name="T">Type of the value</typeparam>
    public void WriteAnyRegisteredValueAsObject<T>(T value);

    // ValueTuple handling without boxing
    public void WriteObject<T1, T2>(in (T1 Value1, T2 Value2) tuple);
    public void WriteObject<T1, T2, T3>(in (T1 Value1, T2 Value2, T3 Value3) tuple);
    public void WriteObject<T1, T2, T3, T4>(in (T1 Value1, T2 Value2, T3 Value3, T4 Value4) tuple);

    // Reference tuples are always less efficient than value tuples, but specifying these makes the API a bit nicer
    public void WriteObject<T1, T2>(Tuple<T1, T2> tuple);
    public void WriteObject<T1, T2, T3>(Tuple<T1, T2, T3> tuple);
    public void WriteObject<T1, T2, T3, T4>(Tuple<T1, T2, T3, T4> tuple);

    /// <summary>
    ///   Other types of tuples are supported generically and are less performant.
    /// </summary>
    /// <param name="tuple">Any kind of tuple to write (may cause boxing for ValueTuple)</param>
    /// <param name="valueType">Indicate whether the original tuple is a value tuple or a reference tuple</param>
    public void WriteObject(ITuple tuple, bool valueType);

    /// <summary>
    ///   Writes most kinds of lists
    /// </summary>
    public void WriteObject<T>(IList<T> list);

    public void WriteObject<T>(T[] array);

    /// <summary>
    ///   Writes all kinds of lists.
    ///   When the type is not known, this is less efficient as this needs to use reflection to find the
    ///   actual object type in the list.
    /// </summary>
    public void WriteUnknownList(IList list);

    public void WriteObject<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> dictionary);

    /// <summary>
    ///   Writes the object header and then a null value.
    /// </summary>
    public void WriteNullObject();

    /// <summary>
    ///   Writes an overall header for an entire archive. Call before writing any objects to a new archive.
    /// </summary>
    /// <param name="overallVersion">
    ///   Overall version of the archive (usually from <see cref="ArchiveHeaderVersion"/>
    /// </param>
    /// <param name="programIdentifier">Program identifier to identify archives from different programs</param>
    /// <param name="programVersion">Free-form program version information</param>
    public void WriteArchiveHeader(int overallVersion, string programIdentifier, string programVersion);

    public void WriteArchiveFooter();

    public long GetPosition();
    public void Seek(long position);
}
