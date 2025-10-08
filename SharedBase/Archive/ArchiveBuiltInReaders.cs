namespace SharedBase.Archive;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public static class ArchiveBuiltInReaders
{
    private static readonly Dictionary<Type, FieldInfo[]> TupleTypeFieldCache = new();
    private static readonly Dictionary<FieldInfo, bool> FieldNullabilityCache = new();

    private static readonly Type IntType = typeof(int);
    private static readonly Type BoolType = typeof(bool);
    private static readonly Type LongType = typeof(long);
    private static readonly Type ULongType = typeof(ulong);
    private static readonly Type StringType = typeof(string);

    public static object ReadReferenceTuple(ISArchiveReader reader, ushort version)
    {
        if (version > SArchiveWriterBase.TUPLE_VERSION)
            throw new InvalidArchiveVersionException(version, SArchiveWriterBase.TUPLE_VERSION);

        var length = reader.ReadInt8();

        if (length < 1)
            throw new FormatException("Tuple length must be at least 1");

        if (length > 7)
            throw new FormatException("Only tuples up to 7 items long are supported");

        // TODO: improve memory usage here, as this seems very inefficient in terms of allocations
        var rawValues = new object?[length];
        var types = new Type[length];

        // Read all the values for the tuple
        for (var i = 0; i < length; i++)
        {
            var value = reader.ReadObject(out var type);
            rawValues[i] = value;

            // If the value is null, we need to know the type, and for that we use the type
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

        // Build the tuple based on the arguments
        var tupleType = MakeGenericTuple(types);

        var constructor = tupleType.GetConstructor(types) ??
            throw new FormatException(
                $"Tuple constructor not found for {string.Join(", ", types.Select(t => t.FullName))}");

        var tuple = constructor.Invoke(rawValues);

        return tuple;
    }

    public static void ReadValueTuple<T>(ref T receiver, int length, ISArchiveReader reader)
        where T : ITuple
    {
        if (length is < 1 or > 7)
            throw new FormatException($"Invalid tuple count for ValueTuple ({length})");

        var receiverType = receiver.GetType();

        // Match to see if the receiver is the same length as the tuple
        if (length != receiverType.GenericTypeArguments.Length)
        {
            throw new FormatException(
                $"Cannot read tuple with length {length} into receiver of type {receiver.GetType()}");
        }

        // It seems like there's no perfect solution that doesn't allocate memory. This current approach uses
        // reflection to get into the fields of the type and processing each with a known set of types. Other types
        // can kind of be used, but they run into the major problem of memory allocation.
        FieldInfo[]? fields;

        lock (TupleTypeFieldCache)
        {
            if (!TupleTypeFieldCache.TryGetValue(receiverType, out fields))
            {
                fields = receiverType.GetFields();
                TupleTypeFieldCache[receiverType] = fields;
            }
        }

        if (fields.Length != length)
            throw new InvalidOperationException("Tuple field count has changed in the C# runtime");

        ref byte baseRef = ref Unsafe.As<T, byte>(ref receiver);

        int nextTupleField = 0;

        // Read tuple members from the archive one by one and hopefully the types match
        for (int i = 0; i < length; ++i)
        {
            var target = fields[nextTupleField++];

            var type = target.FieldType;

            if (type == IntType)
            {
                ReadTupleValue<int>(target, ref baseRef, reader);
            }
            else if (type == BoolType)
            {
                ReadTupleValue<bool>(target, ref baseRef, reader);
            }
            else if (type == LongType)
            {
                ReadTupleValue<long>(target, ref baseRef, reader);
            }
            else if (type == ULongType)
            {
                ReadTupleValue<ulong>(target, ref baseRef, reader);
            }
            else if (type == StringType)
            {
                ReadTupleValue<string>(target, ref baseRef, reader);
            }

            // Otherwise we cannot use non-boxing conversion, so we need to use the generic method
            if (target.FieldType.IsValueType)
            {
                throw new FormatException(
                    "Tuple value type encountered that is not supported but it is a value type, " +
                    "so not doing a boxing conversion");
            }

            // TODO: this needs a test to verify this actually sticks (and some optimization for how badly this allocates)

            target.SetValueDirect(TypedReference.MakeTypedReference(receiver, [
                    fields[nextTupleField - 1],
                ]),
                reader.ReadObject(out _) ??
                new FormatException($"Read a null value but cannot put it into a ValueTuple {typeof(T)}"));
        }
    }

    private static void ReadTupleValue<TField>(FieldInfo fieldInfo, ref byte baseOffset, ISArchiveReader reader)
    {
        var fieldOffset =
            Marshal.OffsetOf(fieldInfo.DeclaringType ?? throw new Exception("Field has no declaring type"),
                fieldInfo.Name);
        ref var fieldRaw = ref Unsafe.Add(ref baseOffset, fieldOffset);
        ref var field = ref Unsafe.As<byte, TField>(ref fieldRaw);

        if (fieldInfo.FieldType.IsValueType)
        {
            reader.ReadAnyStruct(ref field);
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
                throw new FormatException($"Could not convert {newValue} to {typeof(TField)} (archive type: {type})",
                    e);
            }

            if (ReferenceEquals(converted, null) && !IsMarkedAsNullable(fieldInfo))
            {
                throw new FormatException(
                    $"Field {fieldInfo.Name} is not nullable, but the archive contained a null value (when reading " +
                    $"a tuple value)");
            }

            field = converted!;
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
}
