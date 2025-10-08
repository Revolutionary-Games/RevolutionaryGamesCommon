namespace SharedBase.Archive;

using System;
using System.Linq;

public static class ArchiveBuiltInReaders
{
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
