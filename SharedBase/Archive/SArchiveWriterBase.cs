namespace SharedBase.Archive;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

/// <summary>
///   Abstract base with common methods for archive writers
/// </summary>
public abstract class SArchiveWriterBase : ISArchiveWriter
{
    public const ushort TUPLE_VERSION = 1;
    public const ushort COLLECTIONS_VERSION = 1;

    private const int BUFFER_SIZE = 1024;

    private Encoder? textEncoder;
    private byte[]? scratch;

    protected SArchiveWriterBase(IArchiveWriteManager writeManager)
    {
        WriteManager = writeManager;

#if DEBUG
        if (!BitConverter.IsLittleEndian)
        {
            throw new PlatformNotSupportedException("This library supports only little-endian");
        }
#endif
    }

    public IArchiveWriteManager WriteManager { get; protected set; }

    public abstract void Write(byte value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(short value)
    {
        Write((byte)value);
        Write((byte)(value >> 8));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(int value)
    {
        Write((byte)value);
        Write((byte)(value >> 8));
        Write((byte)(value >> 16));
        Write((byte)(value >> 24));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(long value)
    {
        Write((byte)value);
        Write((byte)(value >> 8));
        Write((byte)(value >> 16));
        Write((byte)(value >> 24));
        Write((byte)(value >> 32));
        Write((byte)(value >> 40));
        Write((byte)(value >> 48));
        Write((byte)(value >> 56));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(float value)
    {
        Write(BitConverter.SingleToInt32Bits(value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(double value)
    {
        Write(BitConverter.DoubleToInt64Bits(value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(ushort value)
    {
        Write((byte)value);
        Write((byte)(value >> 8));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(uint value)
    {
        Write((byte)value);
        Write((byte)(value >> 8));
        Write((byte)(value >> 16));
        Write((byte)(value >> 24));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(ulong value)
    {
        Write((byte)value);
        Write((byte)(value >> 8));
        Write((byte)(value >> 16));
        Write((byte)(value >> 24));
        Write((byte)(value >> 32));
        Write((byte)(value >> 40));
        Write((byte)(value >> 48));
        Write((byte)(value >> 56));
    }

    public virtual void Write(string? value)
    {
        if (value == null)
        {
            WriteVariableLengthField32(0);
            return;
        }

        if ((ulong)value.Length > uint.MaxValue >> 1)
            throw new ArgumentException("String is too long");

        var converted = (uint)value.Length;

        // The first bit is reserved for marking null strings. So for non-null it must be 1.
        converted = (converted << 1) | 0x1;

        WriteVariableLengthField32(converted);

        if (value.Length < 1)
            return;

        textEncoder ??= ISArchiveWriter.Utf8NoSignature.GetEncoder();
        var encoder = textEncoder;

        scratch ??= new byte[BUFFER_SIZE];
        Span<byte> buffer = scratch;

        ReadOnlySpan<char> chars = value.AsSpan();

        // Encode the string in parts to avoid allocating a large buffer
        int charIndex = 0;
        while (charIndex < chars.Length)
        {
            encoder.Convert(chars.Slice(charIndex), buffer,
                false, out int charsUsed, out int bytesUsed, out _);

            Write(buffer[..bytesUsed]);

            charIndex += charsUsed;
        }

        // Flush any remaining data
        encoder.Convert(ReadOnlySpan<char>.Empty, buffer,
            true, out _, out int finalBytes, out _);

        if (finalBytes > 0)
            Write(buffer[..finalBytes]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write(byte[] value)
    {
        Write(value.AsSpan());
    }

    public abstract void Write(ReadOnlySpan<byte> value);

    public void WriteVariableLengthField32(uint value)
    {
        while (true)
        {
            // Stop when written all bytes
            if ((value & 0xFFFFFF80) == 0)
                break;

            // 7 bits with the continuation flag set
            var currentByte = (byte)(value & 0x7F | 0x80);
            Write(currentByte);

            value >>= 7;
        }

        // Write the last byte without the continuation flag
        Write((byte)value);
    }

    public void WriteObjectHeader(ArchiveObjectType type, bool canBeReference, bool isNull, bool usesExistingReference,
        ushort version)
    {
        if (type > ArchiveObjectType.LastValidObjectType)
            throw new ArgumentException("Invalid object type (value too high)");

        if (version <= 0)
            throw new ArgumentException("Version must be at least 1");

        bool versionIsLong = version > 0x7;

        // The object type takes at most 24 bits, so we have 8 bits for flags at the start
        // If the version takes more than 4 bits, we write a separate field for it
        uint archiveValue = (uint)type << 8 | (uint)(canBeReference ? 0x1 : 0) | (uint)(isNull ? 0x2 : 0) |
            (uint)(usesExistingReference ? 0x4 : 0) |
            (uint)((version & 0x7) << 4) | (uint)(versionIsLong ? 0x80 : 0);

        Write(archiveValue);

        if (versionIsLong && !isNull)
        {
            Write(version);
        }
    }

    public void WriteObject(IArchivable obj)
    {
        bool canBeReference = obj.CanBeReferencedInArchive;
        WriteObjectHeader(obj.ArchiveObjectType, canBeReference, false,
            canBeReference && WriteManager.IsReferencedAlready(obj),
            obj.CurrentArchiveVersion);

        // If the object can be a reference, we write a placeholder for it, if it stays all 0, then it was not actually
        // referenced
        if (canBeReference)
        {
            // The WriteManager handles writing the reference data
            if (WriteManager.MarkStartOfReferenceObject(this, obj))
            {
                // Object was already written, so we don't need to write it again, and the manager already put the ID
                // here, so we don't even need to write that
                return;
            }
        }

        // Header handled, let the object handle saving its data
        obj.WriteToArchive(this);
    }

    public void WriteObject<T>(ref T obj)
        where T : struct, IArchivable
    {
        WriteObjectHeader(obj.ArchiveObjectType, false, false, false, obj.CurrentArchiveVersion);

        // Header handled, let the object handle saving its data
        obj.WriteToArchive(this);
    }

    public void WriteObject<T>(IList<T> list)
    {
        WriteObjectHeader(ArchiveObjectType.List, false, false, false, COLLECTIONS_VERSION);

        // Write list length first
        WriteVariableLengthField32((uint)list.Count);

        // Optimisation for some primitive types
        if (WriteOptimizedListIfPossible(list))
        {
            return;
        }

        Write((uint)WriteManager.GetObjectWriteType(typeof(T)));
        Write((byte)0);

        int count = list.Count;
        for (int i = 0; i < count; ++i)
        {
            WriteAnyRegisteredValueAsObject(list[i]);
        }
    }

    public void WriteUnknownList(IList list)
    {
        WriteObjectHeader(ArchiveObjectType.List, false, false, false, COLLECTIONS_VERSION);

        // Write list length first
        WriteVariableLengthField32((uint)list.Count);

        // Optimisation for some primitive types
        if (WriteOptimizedListIfPossible(list))
        {
            return;
        }

        Write((uint)WriteManager.GetObjectWriteType(list.GetType().GetGenericArguments()[0]));
        Write((byte)0);

        int count = list.Count;
        for (int i = 0; i < count; ++i)
        {
            WriteAnyRegisteredValueAsObject(list[i]);
        }
    }

    public void WriteObject<T>(T[] array)
    {
        // Automatic byte array optimisation
        if (array is byte[] byteArray)
        {
            WriteObjectHeader(ArchiveObjectType.ByteArray, false, false, false, 1);
            Write(byteArray);
            return;
        }

        WriteObjectHeader(ArchiveObjectType.Array, false, false, false, COLLECTIONS_VERSION);

        WriteVariableLengthField32((uint)array.Length);

        // Optimisation for some primitive types
        if (WriteOptimizedArrayIfPossible(array))
        {
            return;
        }

        Write((uint)WriteManager.GetObjectWriteType(typeof(T)));
        Write((byte)0);

        int count = array.Length;
        for (int i = 0; i < count; ++i)
        {
            WriteAnyRegisteredValueAsObject(array[i]);
        }
    }

    public void WriteObject<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> dictionary)
    {
        WriteObjectHeader(ArchiveObjectType.Dictionary, false, false, false, COLLECTIONS_VERSION);

        WriteVariableLengthField32((uint)dictionary.Count);

        // TODO: specific kind of dictionary speed hacks?
        Write((byte)0);

        // Key type
        Write((uint)WriteManager.GetObjectWriteType(typeof(TKey)));

        // And value type
        Write((uint)WriteManager.GetObjectWriteType(typeof(TValue)));

        // And then each key pair item
        foreach (var pair in dictionary)
        {
            WriteAnyRegisteredValueAsObject(pair.Key);
            WriteAnyRegisteredValueAsObject(pair.Value);
        }
    }

    /// <summary>
    ///   Writes anything that kind of seems like a list. Much less efficient than a known type of collection writing.
    /// </summary>
    public void WriteObject<T>(IEnumerable anyEnumerable)
    {
        throw new NotImplementedException();
    }

    public void WriteAnyRegisteredValueAsObject<T>(T value)
    {
        if (ReferenceEquals(value, null))
        {
            WriteNullObject();
            return;
        }

        // Primitive matching
        switch (value)
        {
            case bool boolValue:
                WriteObjectHeader(ArchiveObjectType.Bool, false, false, false, 1);
                Write(boolValue ? (byte)1 : (byte)0);
                return;
            case byte byteValue:
                WriteObjectHeader(ArchiveObjectType.Byte, false, false, false, 1);
                Write(byteValue);
                return;
            case short shortValue:
                WriteObjectHeader(ArchiveObjectType.Int16, false, false, false, 1);
                Write(shortValue);
                return;
            case int intValue:
                WriteObjectHeader(ArchiveObjectType.Int32, false, false, false, 1);
                Write(intValue);
                return;
            case long longValue:
                WriteObjectHeader(ArchiveObjectType.Int64, false, false, false, 1);
                Write(longValue);
                return;
            case float floatValue:
                WriteObjectHeader(ArchiveObjectType.Float, false, false, false, 1);
                Write(floatValue);
                return;
            case double doubleValue:
                WriteObjectHeader(ArchiveObjectType.Double, false, false, false, 1);
                Write(doubleValue);
                return;

            case ushort ushortValue:
                WriteObjectHeader(ArchiveObjectType.UInt16, false, false, false, 1);
                Write(ushortValue);
                return;
            case uint uintValue:
                WriteObjectHeader(ArchiveObjectType.UInt32, false, false, false, 1);
                Write(uintValue);
                return;
            case ulong ulongValue:
                WriteObjectHeader(ArchiveObjectType.UInt64, false, false, false, 1);
                Write(ulongValue);
                return;
            case byte[] byteArrayValue:
                WriteObjectHeader(ArchiveObjectType.ByteArray, false, false, false, 1);
                WriteVariableLengthField32((uint)byteArrayValue.Length);
                Write(byteArrayValue);
                return;

            case string stringValue:
                WriteObjectHeader(ArchiveObjectType.String, false, false, false, 1);
                Write(stringValue);
                return;
        }

        // TODO: solve the case of using boxing for struct values that these casts may cause
        if (value is IArchivable archivable)
        {
            WriteObject(archivable);
            return;
        }

        // This is meant for full values, as such this does not support IArchiveUpdatable

        // TODO: solve this boxing tuple problem (or not, as this doesn't get highlighted as allocating memory)
        if (value is ITuple tuple)
        {
            bool valueType = value.GetType().IsValueType;
            WriteObject(tuple, valueType);
            return;
        }

        if (value is IList list)
        {
            WriteUnknownList(list);
            return;
        }

        throw new FormatException($"No known conversion for type {typeof(T).FullName} into an archive");
    }

    public void WriteObjectProperties<T>(ref T obj)
        where T : IArchiveUpdatable
    {
        WriteObjectHeader(obj.ArchiveObjectType, false, false, false, obj.CurrentArchiveVersion);

        obj.WritePropertiesToArchive(this);
    }

    public void WriteObject<T1, T2>(in (T1 Value1, T2 Value2) tuple)
    {
        WriteObjectHeader(ArchiveObjectType.Tuple, false, false, false, TUPLE_VERSION);

        // Length of the tuple
        Write((byte)2);

        // And then the items
        WriteAnyRegisteredValueAsObject(tuple.Value1);
        WriteAnyRegisteredValueAsObject(tuple.Value2);
    }

    public void WriteObject<T1, T2, T3>(in (T1 Value1, T2 Value2, T3 Value3) tuple)
    {
        WriteObjectHeader(ArchiveObjectType.Tuple, false, false, false, TUPLE_VERSION);

        // Length of the tuple
        Write((byte)3);

        // And then the items
        WriteAnyRegisteredValueAsObject(tuple.Value1);
        WriteAnyRegisteredValueAsObject(tuple.Value2);
        WriteAnyRegisteredValueAsObject(tuple.Value3);
    }

    public void WriteObject<T1, T2, T3, T4>(in (T1 Value1, T2 Value2, T3 Value3, T4 Value4) tuple)
    {
        WriteObjectHeader(ArchiveObjectType.Tuple, false, false, false, TUPLE_VERSION);

        // Length of the tuple
        Write((byte)4);

        // And then the items
        WriteAnyRegisteredValueAsObject(tuple.Value1);
        WriteAnyRegisteredValueAsObject(tuple.Value2);
        WriteAnyRegisteredValueAsObject(tuple.Value3);
        WriteAnyRegisteredValueAsObject(tuple.Value4);
    }

    // Other tuples are supported generically and are less performant
    public void WriteObject(ITuple tuple, bool valueType)
    {
        // To preserve the tuple type even if it goes through the generic method, we write the header type here
        WriteObjectHeader(valueType ? ArchiveObjectType.Tuple : ArchiveObjectType.ReferenceTuple, false, false, false,
            TUPLE_VERSION);

        if (tuple.Length > byte.MaxValue)
            throw new FormatException("Too long tuple type");

        // Length of the tuple
        var length = tuple.Length;
        Write((byte)length);

        // And then the items
        for (int i = 0; i < length; ++i)
        {
            WriteAnyRegisteredValueAsObject(tuple[i]);
        }
    }

    // Reference tuples are always less efficient than value tuples, but specifying these makes the API a bit nicer
    public void WriteObject<T1, T2>(Tuple<T1, T2> tuple)
    {
        WriteObject(tuple, false);
    }

    public void WriteObject<T1, T2, T3>(Tuple<T1, T2, T3> tuple)
    {
        WriteObject(tuple, false);
    }

    public void WriteObject<T1, T2, T3, T4>(Tuple<T1, T2, T3, T4> tuple)
    {
        WriteObject(tuple, false);
    }

    public void WriteNullObject()
    {
        WriteObjectHeader(ArchiveObjectType.Null, false, true, false, 1);

        // Nulls do not have a reference placeholder, even if they can be otherwise references
    }

    public abstract long GetPosition();
    public abstract void Seek(long position);

    private bool WriteOptimizedListIfPossible(object list)
    {
        if (list is IReadOnlyList<int> intList)
        {
            // Write the list element type
            Write((uint)ArchiveObjectType.Int32);
            Write((byte)1);

            int count = intList.Count;
            for (int i = 0; i < count; ++i)
            {
                Write(intList[i]);
            }
        }
        else if (list is IReadOnlyList<byte> byteList)
        {
            Write((uint)ArchiveObjectType.Byte);
            Write((byte)1);

            int count = byteList.Count;
            for (int i = 0; i < count; ++i)
            {
                Write(byteList[i]);
            }
        }
        else if (list is IReadOnlyList<long> longList)
        {
            Write((uint)ArchiveObjectType.Int64);
            Write((byte)1);

            int count = longList.Count;
            for (int i = 0; i < count; ++i)
            {
                Write(longList[i]);
            }
        }
        else if (list is IReadOnlyList<bool> boolList)
        {
            Write((uint)ArchiveObjectType.Bool);
            Write((byte)1);

            int count = boolList.Count;
            for (int i = 0; i < count; ++i)
            {
                Write(boolList[i] ? (byte)1 : (byte)0);
            }
        }
        else if (list is IReadOnlyList<string> stringList)
        {
            Write((uint)ArchiveObjectType.String);
            Write((byte)1);

            int count = stringList.Count;
            for (int i = 0; i < count; ++i)
            {
                Write(stringList[i]);
            }
        }
        else if (list is IReadOnlyList<float> floatList)
        {
            Write((uint)ArchiveObjectType.Float);
            Write((byte)1);

            int count = floatList.Count;
            for (int i = 0; i < count; ++i)
            {
                Write(floatList[i]);
            }
        }
        else
        {
            return false;
        }

        return true;
    }

    private bool WriteOptimizedArrayIfPossible(object array)
    {
        if (array is int[] intArray)
        {
            Write((uint)ArchiveObjectType.Int32);
            Write((byte)1);

            for (int i = 0; i < intArray.Length; ++i)
            {
                Write(intArray[i]);
            }
        }
        else if (array is byte[] byteArray)
        {
            Write((uint)ArchiveObjectType.Byte);
            Write((byte)1);

            for (int i = 0; i < byteArray.Length; ++i)
            {
                Write(byteArray[i]);
            }
        }
        else if (array is long[] longArray)
        {
            Write((uint)ArchiveObjectType.Int64);
            Write((byte)1);

            for (int i = 0; i < longArray.Length; ++i)
            {
                Write(longArray[i]);
            }
        }
        else if (array is bool[] boolArray)
        {
            Write((uint)ArchiveObjectType.Bool);
            Write((byte)1);

            for (int i = 0; i < boolArray.Length; ++i)
            {
                Write(boolArray[i] ? (byte)1 : (byte)0);
            }
        }
        else if (array is string[] stringArray)
        {
            Write((uint)ArchiveObjectType.String);
            Write((byte)1);

            for (int i = 0; i < stringArray.Length; ++i)
            {
                Write(stringArray[i]);
            }
        }
        else if (array is float[] floatArray)
        {
            Write((uint)ArchiveObjectType.Float);
            Write((byte)1);

            for (int i = 0; i < floatArray.Length; ++i)
            {
                Write(floatArray[i]);
            }
        }
        else
        {
            return false;
        }

        return true;
    }
}
