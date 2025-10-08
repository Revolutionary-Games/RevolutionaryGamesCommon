namespace SharedBase.Archive;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

public static class ArchiveBuiltInReaders
{
    private static readonly Dictionary<FieldInfo, bool> FieldNullabilityCache = new();

    public static object ReadReferenceTuple(ISArchiveReader reader, ushort version)
    {
        if (version > SArchiveWriterBase.TUPLE_VERSION)
            throw new InvalidArchiveVersionException(version, SArchiveWriterBase.TUPLE_VERSION);

        var length = reader.ReadInt8();

        if (length is < 1 or > 7)
            throw new FormatException($"Reference tuple length must be between 1 and 7, tied to use {length}");

        ReadBoxedTupleValues(reader, length, out var rawValues, out var types);

        // Build the tuple based on the arguments
        var tupleType = MakeGenericTuple(types);

        // TODO: caching for the constructor?
        var constructor = tupleType.GetConstructor(types) ??
            throw new FormatException(
                $"Tuple constructor not found for {string.Join(", ", types.Select(t => t.FullName))}");

        var tuple = constructor.Invoke(rawValues);

        return tuple;
    }

    /// <summary>
    ///   Less efficient variant of tuple reading that creates a boxed instance of the tuple (and also boxes the
    ///   arguments)
    /// </summary>
    public static object ReadValueTupleBoxed(ISArchiveReader reader, ushort version)
    {
        if (version > SArchiveWriterBase.TUPLE_VERSION)
            throw new InvalidArchiveVersionException(version, SArchiveWriterBase.TUPLE_VERSION);

        var length = reader.ReadInt8();

        if (length is < 1 or > 7)
            throw new FormatException($"ValueTuple length must be between 1 and 7, tied to use {length}");

        ReadBoxedTupleValues(reader, length, out var rawValues, out var types);

        // Build the tuple based on the arguments
        var tupleType = MakeGenericValueTuple(types);

        // TODO: caching for the constructor?
        var constructor = tupleType.GetConstructor(types) ??
            throw new FormatException(
                $"ValueTuple constructor not found for {string.Join(", ", types.Select(t => t.FullName))}");

        var tuple = constructor.Invoke(rawValues);

        return tuple;
    }

    /// <summary>
    ///   More efficient variant of tuple reading when the real target type is known.
    /// </summary>
    public static void ReadValueTuple<T1>(ref ValueTuple<T1> receiver, int length, ISArchiveReader reader)
    {
        var receiverType = receiver.GetType();
        CheckTupleLength(length, 1, receiverType);

        ReadTupleValue(ref receiver.Item1, reader, 0, receiverType);
    }

    public static void ReadValueTuple<T1, T2>(ref (T1 Item1, T2 Item2) receiver, int length, ISArchiveReader reader)
    {
        var receiverType = receiver.GetType();
        CheckTupleLength(length, 2, receiverType);

        ReadTupleValue(ref receiver.Item1, reader, 0, receiverType);
        ReadTupleValue(ref receiver.Item2, reader, 1, receiverType);
    }

    public static void ReadValueTuple<T1, T2, T3>(ref (T1 Item1, T2 Item2, T3 Item3) receiver, int length,
        ISArchiveReader reader)
    {
        var receiverType = receiver.GetType();
        CheckTupleLength(length, 3, receiverType);

        ReadTupleValue(ref receiver.Item1, reader, 0, receiverType);
        ReadTupleValue(ref receiver.Item2, reader, 1, receiverType);
        ReadTupleValue(ref receiver.Item3, reader, 2, receiverType);
    }

    public static void ReadValueTuple<T1, T2, T3, T4>(ref (T1 Item1, T2 Item2, T3 Item3, T4 Item4) receiver, int length,
        ISArchiveReader reader)
    {
        var receiverType = receiver.GetType();
        CheckTupleLength(length, 4, receiverType);

        ReadTupleValue(ref receiver.Item1, reader, 0, receiverType);
        ReadTupleValue(ref receiver.Item2, reader, 1, receiverType);
        ReadTupleValue(ref receiver.Item3, reader, 2, receiverType);
        ReadTupleValue(ref receiver.Item4, reader, 3, receiverType);
    }

    private static void ReadTupleValue<TField>(ref TField fieldValue, ISArchiveReader reader, int fieldIndex,
        Type receiverType)
    {
        try
        {
            if (typeof(TField).IsValueType)
            {
                reader.ReadAnyStruct(ref fieldValue);
            }
            else
            {
                var newValue = reader.ReadObject(out var type);

                TField? converted;
                try
                {
                    converted = (TField?)newValue;
                }
                catch (Exception e)
                {
                    throw new FormatException(
                        $"Could not convert {newValue} to {typeof(TField)} (archive type: {type})",
                        e);
                }

                // TODO: should we do null verification here? (we'd need to read the field info of the type)
                /*if (ReferenceEquals(converted, null) && !IsMarkedAsNullable(fieldInfo))
                {
                    throw new FormatException(
                        $"Field {fieldIndex} is not nullable in {receiverType}, but the archive contained a " +
                        "null value (when reading a tuple value)");
                }*/

                fieldValue = converted!;
            }
        }
        catch (Exception e)
        {
            throw new FormatException($"Could not read tuple value {fieldIndex} of {receiverType}", e);
        }
    }

    private static void ReadBoxedTupleValues(ISArchiveReader reader, int length, out object?[] rawValues,
        out Type[] types)
    {
        // TODO: improve memory usage here, as this seems very inefficient in terms of allocations
        rawValues = new object?[length];
        types = new Type[length];

        // Read all the values for the tuple
        for (var i = 0; i < length; i++)
        {
            var value = reader.ReadObject(out var type);
            rawValues[i] = value;

            // If the value is null, we need to know the type, and for that we use the type from reader registration
            if (value == null)
            {
                types[i] = reader.MapArchiveTypeToType(type) ??
                    throw new FormatException(
                        $"Reading tuple values had a null and no Type was registered for: {type}");
            }
            else
            {
                types[i] = value.GetType();
            }
        }
    }

    private static bool IsMarkedAsNullable(FieldInfo fieldInfo)
    {
        lock (FieldNullabilityCache)
        {
            if (FieldNullabilityCache.TryGetValue(fieldInfo, out var result))
                return result;

            result = new NullabilityInfoContext().Create(fieldInfo).WriteState is NullabilityState.Nullable;
            FieldNullabilityCache[fieldInfo] = result;
            return result;
        }
    }

    // TODO: would caching for this help?
    private static Type MakeGenericTuple(Type[] argumentTypes)
    {
        return argumentTypes.Length switch
        {
            1 => typeof(Tuple<>).MakeGenericType(argumentTypes),
            2 => typeof(Tuple<,>).MakeGenericType(argumentTypes),
            3 => typeof(Tuple<,,>).MakeGenericType(argumentTypes),
            4 => typeof(Tuple<,,,>).MakeGenericType(argumentTypes),
            5 => typeof(Tuple<,,,,>).MakeGenericType(argumentTypes),
            6 => typeof(Tuple<,,,,,>).MakeGenericType(argumentTypes),
            7 => typeof(Tuple<,,,,,,>).MakeGenericType(argumentTypes),
            _ => throw new NotSupportedException(),
        };
    }

    private static Type MakeGenericValueTuple(Type[] argumentTypes)
    {
        return argumentTypes.Length switch
        {
            1 => typeof(ValueTuple<>).MakeGenericType(argumentTypes),
            2 => typeof(ValueTuple<,>).MakeGenericType(argumentTypes),
            3 => typeof(ValueTuple<,,>).MakeGenericType(argumentTypes),
            4 => typeof(ValueTuple<,,,>).MakeGenericType(argumentTypes),
            5 => typeof(ValueTuple<,,,,>).MakeGenericType(argumentTypes),
            6 => typeof(ValueTuple<,,,,,>).MakeGenericType(argumentTypes),
            7 => typeof(ValueTuple<,,,,,,>).MakeGenericType(argumentTypes),
            _ => throw new NotSupportedException(),
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CheckTupleLength(int length, int expectedLength, Type receiverType)
    {
        if (length != expectedLength)
            throw new FormatException($"Invalid tuple count ({length}) for {receiverType}");
    }
}
