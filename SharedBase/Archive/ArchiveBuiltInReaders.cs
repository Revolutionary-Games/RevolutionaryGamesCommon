namespace SharedBase.Archive;

using System;
using System.Collections;
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

    public static object ReadReferenceTupleKnownType(ISArchiveReader reader, Type typeFromArchive, ushort version)
    {
        if (version > SArchiveWriterBase.TUPLE_VERSION)
            throw new InvalidArchiveVersionException(version, SArchiveWriterBase.TUPLE_VERSION);

        var length = reader.ReadInt8();

        if (length is < 1 or > 7)
            throw new FormatException($"ReferenceTuple length must be between 1 and 7, tied to use {length}");

        var rawValues = new object?[length];
        ReadBoxedTupleValuesKnownTypes(reader, rawValues);

        // To pick a constructor specifically, we would need to create a type array of the parameters by reading
        // the type,
        // but that's probably worse than just finding a constructor with the correct number of arguments.

        // TODO: caching this constructor?
        foreach (var constructor in typeFromArchive.GetConstructors())
        {
            // TODO: does the GetParameters call allocate memory here?
            if (constructor.GetParameters().Length == length)
            {
                return constructor.Invoke(rawValues);
            }
        }

        throw new FormatException($"Tuple constructor not found for {typeFromArchive.FullName}, length: {length}");
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

    public static object ReadValueTupleBoxedKnownType(ISArchiveReader reader, Type typeFromArchive, ushort version)
    {
        if (version > SArchiveWriterBase.TUPLE_VERSION)
            throw new InvalidArchiveVersionException(version, SArchiveWriterBase.TUPLE_VERSION);

        var length = reader.ReadInt8();

        if (length is < 1 or > 7)
            throw new FormatException($"ValueTuple length must be between 1 and 7, tied to use {length}");

        var rawValues = new object?[length];
        ReadBoxedTupleValuesKnownTypes(reader, rawValues);

        // To pick a constructor specifically, we would need to create a type array of the parameters by reading
        // the type,
        // but that's probably worse than just finding a constructor with the correct number of arguments.

        // TODO: caching this constructor?
        foreach (var constructor in typeFromArchive.GetConstructors())
        {
            // TODO: does the GetParameters call allocate memory here?
            if (constructor.GetParameters().Length == length)
            {
                return constructor.Invoke(rawValues);
            }
        }

        throw new FormatException($"ValueTuple constructor not found for {typeFromArchive.FullName}, length: {length}");
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

    public static object ReadList(ISArchiveReader reader, ushort version)
    {
        // Note that this is very similar to the array, but at key points different, still it seems quite hard to
        // combine these two methods into one generic base, so these are separate

        if (version > SArchiveWriterBase.COLLECTIONS_VERSION)
            throw new InvalidArchiveVersionException(version, SArchiveWriterBase.COLLECTIONS_VERSION);

        var lengthRaw = reader.ReadVariableLengthField32();

        if (lengthRaw > int.MaxValue)
            throw new FormatException($"List length is too large: {lengthRaw}");

        var length = (int)lengthRaw;

        var rawType = (ArchiveObjectType)reader.ReadUInt32();
        var optimizedPrimitive = reader.ReadBool();

        if (optimizedPrimitive)
        {
            return ReadOptimizedList(reader, rawType, length);
        }

        // Less optimised approach

        // Build the list based on the arguments
        var singleTypeArray = new Type[1];

        var listItemType = reader.MapArchiveTypeToType(rawType) ??
            throw new FormatException("Unregistered type for list items: " + rawType);

        Type listType;

        // If the list item type contains generic parameters, we need to resolve those somehow here
        if (listItemType.IsGenericType)
        {
            // We need to figure out what the actual type is.
            // This is really inefficient, but we'll try to read the items first and then figure out the type to use
            var tempData = new object?[length];

            for (int i = 0; i < length; ++i)
            {
                tempData[i] = reader.ReadObject(out _);
            }

            var target = DetermineCommonObjectType(length, tempData);

            singleTypeArray[0] = target;
            listType = MakeGenericList(singleTypeArray);

            var list2 = CreateBaseList(singleTypeArray, listType, length);

            // Copy the data we already read
            for (int i = 0; i < length; ++i)
            {
                try
                {
                    list2.Add(tempData[i]);
                }
                catch (Exception e)
                {
                    throw new FormatException(
                        $"Cannot copy item to list at index {i}, item is {tempData[i]?.GetType()} " +
                        $"and the list is: {listType}", e);
                }
            }

            if (list2.Count != length)
                throw new Exception("Failed to put the right number of items into a list");

            return list2;
        }

        singleTypeArray[0] = listItemType;
        listType = MakeGenericList(singleTypeArray);

        return CreateAndReadListItems(reader, singleTypeArray, listType, length);
    }

    public static object ReadListKnownType(ISArchiveReader reader, Type typeFromArchive, ushort version)
    {
        if (version > SArchiveWriterBase.COLLECTIONS_VERSION)
            throw new InvalidArchiveVersionException(version, SArchiveWriterBase.COLLECTIONS_VERSION);

        var lengthRaw = reader.ReadVariableLengthField32();

        if (lengthRaw > int.MaxValue)
            throw new FormatException($"List length is too large: {lengthRaw}");

        var length = (int)lengthRaw;

        var rawType = (ArchiveObjectType)reader.ReadUInt32();
        var optimizedPrimitive = reader.ReadBool();

        // If the list is optimized, we ignore the known type and do an optimized read anyway
        if (optimizedPrimitive)
        {
            return ReadOptimizedList(reader, rawType, length);
        }

        // It's probably better to allocate this small list here so that we can call the constructor taking in the list
        // size as a parameter to optimize the list reallocation
        var singleTypeArray = new Type[1];

        return CreateAndReadListItems(reader, singleTypeArray, typeFromArchive, length);
    }

    public static object ReadArray(ISArchiveReader reader, ushort version)
    {
        // Note that this is very similar to the list, but at key points different, still it seems quite hard to
        // combine these two methods into one generic base, so these are separate

        if (version > SArchiveWriterBase.COLLECTIONS_VERSION)
            throw new InvalidArchiveVersionException(version, SArchiveWriterBase.COLLECTIONS_VERSION);

        var lengthRaw = reader.ReadVariableLengthField32();

        if (lengthRaw > int.MaxValue)
            throw new FormatException($"Array length is too large: {lengthRaw}");

        var length = (int)lengthRaw;

        var rawType = (ArchiveObjectType)reader.ReadUInt32();
        var optimizedPrimitive = reader.ReadBool();

        if (optimizedPrimitive)
        {
            switch (rawType)
            {
                case ArchiveObjectType.Int32:
                {
                    var result = new int[length];
                    for (int i = 0; i < length; ++i)
                    {
                        result[i] = reader.ReadInt32();
                    }

                    return result;
                }

                case ArchiveObjectType.Byte:
                {
                    var result = new byte[length];
                    for (int i = 0; i < length; ++i)
                    {
                        result[i] = reader.ReadInt8();
                    }

                    return result;
                }

                case ArchiveObjectType.Int64:
                {
                    var result = new long[length];
                    for (int i = 0; i < length; ++i)
                    {
                        result[i] = reader.ReadInt64();
                    }

                    return result;
                }

                case ArchiveObjectType.String:
                {
                    // We don't know the nullability of the target type, so we need to assume things can be null
                    var result = new string?[length];
                    for (int i = 0; i < length; ++i)
                    {
                        result[i] = reader.ReadString();
                    }

                    return result;
                }

                case ArchiveObjectType.Float:
                {
                    var result = new float[length];
                    for (int i = 0; i < length; ++i)
                    {
                        result[i] = reader.ReadFloat();
                    }

                    return result;
                }

                case ArchiveObjectType.Bool:
                {
                    var result = new bool[length];
                    for (int i = 0; i < length; ++i)
                    {
                        result[i] = reader.ReadBool();
                    }

                    return result;
                }

                default:
                    throw new Exception("Unhandled optimized array primitive read type: " + rawType);
            }
        }

        // Less optimised approach

        // Build the array based on the arguments
        var arrayItemType = reader.MapArchiveTypeToType(rawType) ??
            throw new FormatException("Unregistered type for array items: " + rawType);

        // If the list item type contains generic parameters, we need to resolve those somehow here
        if (arrayItemType.IsGenericType)
        {
            // We need to figure out what the actual type is.
            // This is really inefficient, but we'll try to read the items first and then figure out the type to use
            var tempData = new object?[length];

            for (int i = 0; i < length; ++i)
            {
                tempData[i] = reader.ReadObject(out _);
            }

            var target = DetermineCommonObjectType(length, tempData);

            var array2 = InstantiateArray(MakeArrayType(target), length);

            // Copy the data we already read
            for (int i = 0; i < length; ++i)
            {
                try
                {
                    array2.SetValue(tempData[i], i);
                }
                catch (Exception e)
                {
                    throw new FormatException(
                        $"Cannot copy item to array at index {i}, item is {tempData[i]?.GetType()} " +
                        $"and the array elements are: {target}", e);
                }
            }

            return array2;
        }

        var array = InstantiateArray(MakeArrayType(arrayItemType), length);

        for (int i = 0; i < length; ++i)
        {
            // We need to assume the list will be able to take null values (as we don't know the actual final target)
            var item = reader.ReadObject(out var readItemType);

            try
            {
                array.SetValue(item, i);
            }
            catch (Exception e)
            {
                throw new FormatException(
                    $"Cannot add read item to array at index {i}, item is {item?.GetType()} / {readItemType} " +
                    $"and the array elements are: {arrayItemType}", e);
            }
        }

        return array;
    }

    public static object ReadDictionary(ISArchiveReader reader, ushort version)
    {
        if (version > SArchiveWriterBase.COLLECTIONS_VERSION)
            throw new InvalidArchiveVersionException(version, SArchiveWriterBase.COLLECTIONS_VERSION);

        var lengthRaw = reader.ReadVariableLengthField32();

        if (lengthRaw > int.MaxValue)
            throw new FormatException($"Dictionary length is too large: {lengthRaw}");

        var length = (int)lengthRaw;

        var optimizedPrimitive = reader.ReadBool();

        var rawKeyType = (ArchiveObjectType)reader.ReadUInt32();
        var rawValueType = (ArchiveObjectType)reader.ReadUInt32();

        if (optimizedPrimitive)
            throw new FormatException("Cannot read optimized dictionary primitive");

        var keyType = reader.MapArchiveTypeToType(rawKeyType) ??
            throw new FormatException("Unregistered type for dictionary keys: " + rawKeyType);

        var valueType = reader.MapArchiveTypeToType(rawValueType) ??
            throw new FormatException("Unregistered type for dictionary values: " + rawValueType);

        Type dictionaryType;

        if (NeedsToResolveTypeFurther(keyType) || NeedsToResolveTypeFurther(valueType))
        {
            // Again, reading generically typed things is a lot less efficient than normal reading

            // Fallback if we need it (but may cause pretty horrible boxing and may not be able to be cast easily to the
            // target dictionary kind)
            var items = new object?[length];
            var keys = new object[length];

            for (int i = 0; i < length; ++i)
            {
                keys[i] = reader.ReadObject(out _) ?? throw new FormatException("Null key in dictionary");
                items[i] = reader.ReadObject(out _);
            }

            // Determine the common type for the key and value
            keyType = DetermineCommonObjectType(length, keys);
            valueType = DetermineCommonObjectType(length, items);

            var resolvedTypes = new[] { keyType, valueType };
            dictionaryType = MakeGenericDictionary(resolvedTypes);

            // Then make the dictionary and copy the data we have
            var dictionary2 = CreateDictionary(dictionaryType);

            for (int i = 0; i < length; ++i)
            {
                try
                {
                    dictionary2.Add(keys[i], items[i]);
                }
                catch (Exception e)
                {
                    throw new FormatException(
                        $"Cannot add buffered item to dictionary at index {i}, key is {keys[i].GetType()} " +
                        $"and value is {items[i]?.GetType()} " +
                        $"and the dictionary is: {dictionaryType}", e);
                }
            }

            return dictionary2;
        }

        var typesArray = new[] { keyType, valueType };
        dictionaryType = MakeGenericDictionary(typesArray);

        return CreateAndReadDictionaryItems(reader, dictionaryType, length);
    }

    public static object ReadDictionaryKnownType(ISArchiveReader reader, Type typeFromArchive, ushort version)
    {
        if (version > SArchiveWriterBase.COLLECTIONS_VERSION)
            throw new InvalidArchiveVersionException(version, SArchiveWriterBase.COLLECTIONS_VERSION);

        var lengthRaw = reader.ReadVariableLengthField32();

        if (lengthRaw > int.MaxValue)
            throw new FormatException($"Dictionary length is too large: {lengthRaw}");

        var length = (int)lengthRaw;

        var optimizedPrimitive = reader.ReadBool();

        var rawKeyType = (ArchiveObjectType)reader.ReadUInt32();
        var rawValueType = (ArchiveObjectType)reader.ReadUInt32();

        if (optimizedPrimitive)
            throw new FormatException("Cannot read optimized dictionary primitive");

        // TODO: should this determine if the key types match the resolved type?
        _ = rawKeyType;
        _ = rawValueType;

        // As we already have the type, we can just create the dictionary after reading the preamble and read
        // the content
        return CreateAndReadDictionaryItems(reader, typeFromArchive, length);
    }

    // TODO: would caching for these help?
    public static Type GetReferenceTupleBase(int count)
    {
        return count switch
        {
            1 => typeof(Tuple<>),
            2 => typeof(Tuple<,>),
            3 => typeof(Tuple<,,>),
            4 => typeof(Tuple<,,,>),
            5 => typeof(Tuple<,,,,>),
            6 => typeof(Tuple<,,,,,>),
            7 => typeof(Tuple<,,,,,,>),
            _ => throw new NotSupportedException(),
        };
    }

    public static Type GetValueTupleBase(int count)
    {
        return count switch
        {
            1 => typeof(ValueTuple<>),
            2 => typeof(ValueTuple<,>),
            3 => typeof(ValueTuple<,,>),
            4 => typeof(ValueTuple<,,,>),
            5 => typeof(ValueTuple<,,,,>),
            6 => typeof(ValueTuple<,,,,,>),
            7 => typeof(ValueTuple<,,,,,,>),
            _ => throw new NotSupportedException(),
        };
    }

    private static object CreateAndReadListItems(ISArchiveReader reader, Type[] singleTypeArray, Type listType,
        int length)
    {
        var list = CreateBaseList(singleTypeArray, listType, length);

        return ReadListItems(reader, listType, length, list);
    }

    private static object ReadListItems(ISArchiveReader reader, Type listType, int length, IList list)
    {
        for (int i = 0; i < length; ++i)
        {
            // We need to assume the list will be able to take null values (as we don't know the actual final target)
            var item = reader.ReadObject(out var readItemType);

            try
            {
                list.Add(item);
            }
            catch (Exception e)
            {
                throw new FormatException(
                    $"Cannot add read item to list at index {i}, item is {item?.GetType()} / {readItemType} " +
                    $"and the list is: {listType}", e);
            }
        }

        if (list.Count != length)
            throw new Exception("Failed to put the right number of items into a list");

        return list;
    }

    private static object ReadOptimizedList(ISArchiveReader reader, ArchiveObjectType rawType, int length)
    {
        switch (rawType)
        {
            case ArchiveObjectType.Int32:
            {
                var result = new List<int>(length);
                for (int i = 0; i < length; ++i)
                {
                    result.Add(reader.ReadInt32());
                }

                return result;
            }

            case ArchiveObjectType.Byte:
            {
                var result = new List<byte>(length);
                for (int i = 0; i < length; ++i)
                {
                    result.Add(reader.ReadInt8());
                }

                return result;
            }

            case ArchiveObjectType.Int64:
            {
                var result = new List<long>(length);
                for (int i = 0; i < length; ++i)
                {
                    result.Add(reader.ReadInt64());
                }

                return result;
            }

            case ArchiveObjectType.String:
            {
                // We don't know the nullability of the target type, so we need to assume things can be null
                var result = new List<string?>(length);
                for (int i = 0; i < length; ++i)
                {
                    result.Add(reader.ReadString());
                }

                return result;
            }

            case ArchiveObjectType.Float:
            {
                var result = new List<float>(length);
                for (int i = 0; i < length; ++i)
                {
                    result.Add(reader.ReadFloat());
                }

                return result;
            }

            case ArchiveObjectType.Bool:
            {
                var result = new List<bool>(length);
                for (int i = 0; i < length; ++i)
                {
                    result.Add(reader.ReadBool());
                }

                return result;
            }

            default:
                throw new Exception("Unhandled optimized list primitive read type: " + rawType);
        }
    }

    private static object CreateAndReadDictionaryItems(ISArchiveReader reader, Type dictionaryType, int length)
    {
        // Apparently we can't optimise based on knowing the pair count in advance, so we just need to read them
        var dictionary = CreateDictionary(dictionaryType);

        for (int i = 0; i < length; ++i)
        {
            var key = reader.ReadObject(out var readKeyType) ?? throw new FormatException("Null key in dictionary");
            var value = reader.ReadObject(out var readValueType);

            try
            {
                dictionary.Add(key, value);
            }
            catch (Exception e)
            {
                throw new FormatException(
                    $"Cannot add read item to dictionary at index {i}, key is {key.GetType()} / {readKeyType} " +
                    $"and value is {value?.GetType()} / {readValueType} " +
                    $"and the dictionary is: {dictionaryType}", e);
            }
        }

        return dictionary;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool NeedsToResolveTypeFurther(Type type)
    {
        if (type.IsGenericType)
            return true;

        // We need to specifically resolve tuples further to avoid cast errors
        if (typeof(ITuple).IsAssignableFrom(type))
            return true;

        return false;
    }

    private static Type DetermineCommonObjectType(int length, object?[] tempData)
    {
        bool didSomething = false;
        Type? target = null;
        bool done = false;
        int steps = 0;

        while (!done)
        {
            if (++steps > 1000)
                throw new Exception("Stuck trying to figure out the type of a generic list");

            for (int i = 0; i < length; ++i)
            {
                if (tempData[i] != null && target == null)
                {
                    var itemType = tempData[i]!.GetType();
                    target = itemType;
                    didSomething = true;
                    break;
                }

                if (tempData[i] != null && target != null)
                {
                    // Need to verify all items can match the type or switch to a parent type (that isn't object)
                    var itemType = tempData[i]!.GetType();

                    if (!target.IsAssignableFrom(itemType))
                    {
                        // A problem
                        if (itemType.IsAssignableFrom(target))
                        {
                            // Luckily, we can just swap the types as one is more generic
                            target = itemType;
                            didSomething = true;
                            break;
                        }

                        // We need to find a base class common to both
                        var commonBase = FindCommonBaseType(itemType, target);

                        if (commonBase != typeof(object))
                        {
                            target = commonBase;
                            didSomething = true;
                            break;
                        }

                        // Did not find a common type, this is bad...
                    }
                }

                if (i + 1 == length && target != null)
                {
                    // Type should be fine now
                    done = true;
                    break;
                }
            }

            // If stuck trying to find a parent type
            if (!didSomething)
                throw new FormatException("Cannot figure out generic type parameter for a list");
        }

        if (target == null!)
        {
            throw new FormatException(
                "Cannot determine type for a generically typed list with generic items when the list is empty!");
        }

        return target;
    }

    private static IList CreateBaseList(Type[] singleTypeArray, Type listType, int length)
    {
        // TODO: caching for the constructor?
        // We want the constructor allowing specifying size upfront
        singleTypeArray[0] = typeof(int);
        var constructor = listType.GetConstructor(singleTypeArray) ??
            throw new FormatException($"List constructor not found for {listType}");

        var singleObjectArray = new object[1];
        singleObjectArray[0] = length;
        var list = (IList)constructor.Invoke(singleObjectArray);
        return list;
    }

    private static Array InstantiateArray(Type type, int length)
    {
        var constructor = type.GetConstructor([typeof(int)]) ?? throw new Exception("Cannot find array constructor");

        var singleObjectArray = new object[1];
        singleObjectArray[0] = length;
        return (Array)constructor.Invoke(singleObjectArray);
    }

    private static IDictionary CreateDictionary(Type dictionaryType)
    {
        // TODO: caching for the constructor?
        // We want the constructor allowing specifying size upfront
        var constructor = dictionaryType.GetConstructor([]) ??
            throw new FormatException($"Dictionary constructor not found for {dictionaryType}");

        var dictionary = (IDictionary)constructor.Invoke([]);
        return dictionary;
    }

    private static Type FindCommonBaseType(Type type1, Type type2)
    {
        var cursor1 = type1.BaseType;

        // First look at base types
        while (cursor1 != null)
        {
            var cursor2 = type2.BaseType;

            while (cursor2 != null)
            {
                if (cursor1.IsAssignableFrom(cursor2))
                {
                    // Ignore the base object type for now
                    if (cursor1 != typeof(object))
                        return cursor1;
                }

                cursor2 = cursor2.BaseType;
            }

            cursor1 = cursor1.BaseType;
        }

        // And then interfaces
        var interfaces1 = type1.GetInterfaces();
        var interfaces2 = type2.GetInterfaces();

        foreach (var interface1 in interfaces1)
        {
            foreach (var interface2 in interfaces2)
            {
                if (interface1.IsAssignableFrom(interface2))
                    return interface1;

                if (interface2.IsAssignableFrom(interface1))
                    return interface2;
            }
        }

        // And if this can't find anything, just return the object type
        return typeof(object);
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
        for (var i = 0; i < length; ++i)
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

    private static void ReadBoxedTupleValuesKnownTypes(ISArchiveReader reader, object?[] rawValues)
    {
        var length = rawValues.Length;

        if (length < 1)
            throw new ArgumentException("Must have at least one value to read");

        // Read all the values for the tuple
        for (var i = 0; i < length; ++i)
        {
            rawValues[i] = reader.ReadObject(out _);
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
        return GetReferenceTupleBase(argumentTypes.Length).MakeGenericType(argumentTypes);
    }

    private static Type MakeGenericValueTuple(Type[] argumentTypes)
    {
        return GetValueTupleBase(argumentTypes.Length).MakeGenericType(argumentTypes);
    }

    private static Type MakeGenericList(Type[] argumentTypes)
    {
        return typeof(List<>).MakeGenericType(argumentTypes);
    }

    private static Type MakeArrayType(Type elementType, int rank = 1)
    {
        return elementType.MakeArrayType(rank);
    }

    private static Type MakeGenericDictionary(Type[] argumentTypes)
    {
        return typeof(Dictionary<,>).MakeGenericType(argumentTypes);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CheckTupleLength(int length, int expectedLength, Type receiverType)
    {
        if (length != expectedLength)
            throw new FormatException($"Invalid tuple count ({length}) for {receiverType}");
    }
}
