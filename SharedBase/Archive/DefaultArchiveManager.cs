namespace SharedBase.Archive;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

/// <summary>
///   Default implementation of the archive manager that handles both reading and writing.
/// </summary>
public class DefaultArchiveManager : IArchiveWriteManager, IArchiveReadManager
{
    public const int ENUM_VERSION = 1;

    // Registered custom types
    // TODO: do we need these write delegates?
    private readonly Dictionary<ArchiveObjectType, IArchiveWriteManager.ArchiveObjectDelegate> writeDelegates = new();
    private readonly Dictionary<ArchiveObjectType, IArchiveReadManager.RestoreObjectDelegate> readDelegates = new();

    private readonly Dictionary<ArchiveObjectType, IArchiveReadManager.AdvancedRestoreObjectDelegate>
        readDelegatesAdvanced = new();

    private readonly Dictionary<ArchiveObjectType, IArchiveReadManager.CreateStructInstanceDelegate>
        readBoxedDelegates = new();

    private readonly Dictionary<ArchiveObjectType, Type> registeredTypes = new();
    private readonly Dictionary<Type, ArchiveObjectType> registeredWriterTypes = new();

    private readonly Dictionary<ArchiveObjectType, ArchiveEnumType> enumTypes = new();

    // Object reference handling
    private readonly Dictionary<object, long> objectIdPositions = new();
    private readonly Dictionary<object, int> objectIds = new();

    /// <summary>
    ///   Used on a load to know what objects are loaded
    /// </summary>
    private readonly Dictionary<int, object> loadedObjectReferences = new();

    private int nextObjectId;

    public DefaultArchiveManager(bool registerDefault = true)
    {
        if (registerDefault)
            IArchiveReadManager.RegisterDefaultObjectReaders(this);
    }

    public void OnStartNewWrite(ISArchiveWriter writer)
    {
        nextObjectId = 1;

        if (objectIdPositions.Count > 0 || objectIds.Count > 0)
        {
            throw new InvalidOperationException(
                "Archive manager has not been properly shutdown since the last started write");
        }
    }

    public void OnFinishWrite(ISArchiveWriter writer)
    {
        // Write actual IDs of used objects
        foreach (var (obj, id) in objectIds)
        {
            if (objectIdPositions.TryGetValue(obj, out var pos))
            {
                writer.Seek(pos);
                writer.Write(id);
            }
            else
            {
                throw new InvalidOperationException($"Cannot find position for an object reference {id}");
            }
        }

        objectIdPositions.Clear();
        objectIds.Clear();
    }

    public bool MarkStartOfReferenceObject(ISArchiveWriter writer, object obj)
    {
        if (objectIdPositions.TryGetValue(obj, out var previousPosition))
        {
            if (nextObjectId == 0)
            {
                throw new InvalidOperationException(
                    "Archive manager has not been initialized! Make sure start / end calls are done");
            }

            // Already using an existing position

            // Need to reserve an ID if not already (as this is the second or further use that refers back to the
            // earlier data)
            if (!objectIds.TryGetValue(obj, out var id))
            {
                if (nextObjectId == int.MaxValue)
                    throw new OverflowException("Ran out of object IDs");

                id = nextObjectId++;
                objectIds[obj] = id;
            }

            // If we are saving things out of order, fail
#if DEBUG
            if (previousPosition >= writer.GetPosition())
                throw new InvalidOperationException("Cannot write object and its reference out of order");
#endif

            // But write the ID that will be used instead of this object
            writer.Write(id);

            return true;
        }

        // Writing a new object, just store the place for the ID, it will only be really used if this object is needed
        // a second time
        objectIdPositions.Add(obj, writer.GetPosition());

        // Otherwise put the placeholder here where we are at the position we just saved
        writer.Write(0);

        return false;
    }

    public bool IsReferencedAlready(object obj)
    {
        return objectIdPositions.ContainsKey(obj);
    }

    public ArchiveObjectType GetObjectWriteType(Type type)
    {
        // Check custom mapping first
        if (registeredWriterTypes.TryGetValue(type, out var value))
            return value;

        // TODO: should this automatically support types that implement IArchiveWritableVariable?

        // Return common types then
        if (type == typeof(byte))
            return ArchiveObjectType.Byte;
        if (type == typeof(bool))
            return ArchiveObjectType.Bool;
        if (type == typeof(short))
            return ArchiveObjectType.Int16;
        if (type == typeof(int))
            return ArchiveObjectType.Int32;
        if (type == typeof(long))
            return ArchiveObjectType.Int64;
        if (type == typeof(ushort))
            return ArchiveObjectType.UInt16;
        if (type == typeof(uint))
            return ArchiveObjectType.UInt32;
        if (type == typeof(ulong))
            return ArchiveObjectType.UInt64;
        if (type == typeof(float))
            return ArchiveObjectType.Float;
        if (type == typeof(double))
            return ArchiveObjectType.Double;
        if (type == typeof(string))
            return ArchiveObjectType.String;

        if (type.IsGenericType)
        {
            var baseType = type.GetGenericTypeDefinition();

            if (baseType == typeof(List<>))
            {
                return ArchiveObjectType.ExtendedList;
            }

            if (baseType == typeof(Dictionary<,>))
            {
                return ArchiveObjectType.ExtendedDictionary;
            }

            if (typeof(IList<>).IsAssignableFrom(baseType))
            {
                return ArchiveObjectType.ExtendedList;
            }

            if (typeof(ITuple).IsAssignableFrom(baseType))
            {
                if (baseType.IsValueType)
                    return ArchiveObjectType.ExtendedTuple;

                return ArchiveObjectType.ExtendedReferenceTuple;
            }
        }

        throw new ArgumentException($"Type is not registered for archive writing: {type}");
    }

    public bool ObjectChildTypeRequiresExtendedType(Type type)
    {
        if (type.IsGenericType)
            return true;

        // TODO: should tuples use extended types? (they already do but is this check needed in some case?)
        /*if (typeof(ITuple).IsAssignableFrom(type))
            return true;*/

        return false;
    }

    public void CalculateExtendedObjectType(ArchiveObjectType baseType, Type type,
        Span<ArchiveObjectType> extendedTypes, out int elementsWritten)
    {
        if (!baseType.IsExtendedType() || !type.IsGenericType)
            throw new ArgumentException("Base type must be an extended type");

        if (extendedTypes.Length < 2)
            throw new ArgumentException("Out of space in extended object type span");

        elementsWritten = 0;

        // We could look up the actual base type again, but for simplicity we just use the given type
        extendedTypes[0] = baseType.RemoveExtendedFlag();
        elementsWritten += 1;

        var generics = type.GetGenericArguments();

        // Tuples must know the generic argument count beforehand
        if (baseType is ArchiveObjectType.ExtendedTuple or ArchiveObjectType.ExtendedReferenceTuple)
        {
            extendedTypes[1] = (ArchiveObjectType)(uint)generics.Length;
            elementsWritten += 1;
        }

        // Then write the child element types
        foreach (var childType in generics)
        {
            var typeToWrite = GetObjectWriteType(childType);
            extendedTypes[elementsWritten] = typeToWrite;
            elementsWritten += 1;

            // If the child type is extended, recursively write out its types as well
            if (typeToWrite.IsExtendedType())
            {
                CalculateExtendedObjectType(typeToWrite, childType,
                    extendedTypes.Slice(elementsWritten, extendedTypes.Length - elementsWritten), out var newElements);

                if (newElements < 1)
                    throw new Exception("Expected recursive call to write something");

                elementsWritten += newElements;
            }
        }

        // Check we didn't write accidentally too much
        if (elementsWritten > ISArchiveWriter.ReasonableMaxExtendedType)
            throw new ArgumentException($"Extended type length too long: {elementsWritten}");
    }

    public void Clear()
    {
        objectIdPositions.Clear();
        objectIds.Clear();

        loadedObjectReferences.Clear();

        nextObjectId = 0;
    }

    public bool WriteCustomEnumIfPossible<T>(ISArchiveWriter writer, T value)
    {
        if (ReferenceEquals(value, null))
            throw new ArgumentException("An enum value cannot be null");

        if (!registeredWriterTypes.TryGetValue(typeof(T), out var type))
            return false;

        if (!enumTypes.TryGetValue(type, out var enumType))
            return false;

        // It is a registered enum type, so we can write it out
        writer.WriteObjectHeader(type, false, false, false, false, ENUM_VERSION);
        writer.Write((byte)enumType);

        switch (enumType)
        {
            case ArchiveEnumType.Int32:
                writer.Write((int)(object)value);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return true;
    }

    public Enum ReadBoxedEnum(ISArchiveReader reader, ArchiveObjectType type, ArchiveEnumType enumType, ushort version)
    {
        if (version is > ENUM_VERSION or <= 0)
            throw new InvalidArchiveVersionException(version, ENUM_VERSION);

        if (!registeredTypes.TryGetValue(type, out var nativeType) || !nativeType.IsEnum)
            throw new ArgumentException($"Type is not a registered enum: {type}");

        var readType = reader.ReadInt8();

        if (enumType != (ArchiveEnumType)readType)
            throw new FormatException($"Enum type mismatch: expected {enumType}, got {readType}");

        switch (enumType)
        {
            case ArchiveEnumType.Int32:
                var intValue = reader.ReadInt32();
                return (Enum)Enum.ToObject(nativeType, intValue);
            default:
                throw new FormatException($"Unimplemented enum read type: {enumType}");
        }
    }

    public Type ResolveExtendedObjectType(ArchiveObjectType baseType, ReadOnlySpan<ArchiveObjectType> extendedType,
        int elementCount, out int consumedItems)
    {
        if (!baseType.IsExtendedType())
            throw new ArgumentException("Base type must be an extended type");

        if (elementCount < 1)
            throw new ArgumentException("Cannot decode empty extended type");

        if (extendedType.Length < elementCount)
            throw new ArgumentException("Span length mismatch");

        consumedItems = 0;
        var nextType = extendedType[consumedItems++];

        var typeToCheckAgainst = nextType;

        if (typeToCheckAgainst.IsExtendedType())
            typeToCheckAgainst = typeToCheckAgainst.RemoveExtendedFlag();

        if (baseType.RemoveExtendedFlag() != typeToCheckAgainst)
        {
            throw new FormatException(
                $"Extended type mismatch, expected to see: {baseType} but read from archive: {nextType}");
        }

        Type? type;

        // Tuple's don't have a base generic class, so we need special handling here
        if (nextType is ArchiveObjectType.ExtendedTuple or ArchiveObjectType.Tuple)
        {
            type = ArchiveBuiltInReaders.GetValueTupleBase((int)extendedType[consumedItems++]);
        }
        else if (nextType is ArchiveObjectType.ExtendedReferenceTuple or ArchiveObjectType.ReferenceTuple)
        {
            type = ArchiveBuiltInReaders.GetReferenceTupleBase((int)extendedType[consumedItems++]);
        }
        else
        {
            type = MapArchiveTypeToType(nextType);
        }

        if (type == null)
            throw new FormatException($"Unknown extended type base: {nextType}");

        if (!type.IsGenericType)
            throw new Exception($"Extended type base is not a generic type: {type}");

        var genericBase = type.GetGenericTypeDefinition();
        var types = new Type[genericBase.GetGenericArguments().Length];

        if (types.Length < 1)
            throw new FormatException($"Extended type base has no generic arguments: {type}");

        for (int i = 0; i < types.Length; ++i)
        {
            if (consumedItems >= elementCount)
                throw new FormatException($"Ran out of extended type elements for {type} at index: {i}");

            var nextElement = extendedType[consumedItems++];

            if (nextElement.IsExtendedType())
            {
                types[i] = ResolveExtendedObjectType(nextElement,
                    extendedType.Slice(consumedItems, extendedType.Length - consumedItems),
                    elementCount - consumedItems, out var read);

                if (read < 1)
                    throw new Exception("Expected recursive call to resolve something");

                consumedItems += read;
            }
            else
            {
                types[i] = MapArchiveTypeToType(nextElement) ??
                    throw new FormatException($"Unknown extended type part ({i}): {nextElement}");
            }
        }

        if (consumedItems > elementCount)
            throw new Exception("Resolving extended type read more items than allowed");

        return genericBase.MakeGenericType(types);
    }

    public void RegisterObjectType(ArchiveObjectType type, Type nativeType,
        IArchiveWriteManager.ArchiveObjectDelegate writeDelegate)
    {
        if (!registeredWriterTypes.TryAdd(nativeType, type) || enumTypes.ContainsKey(type))
            throw new ArgumentException("Type is already registered");

        writeDelegates[type] = writeDelegate;
    }

    public void RegisterObjectType(ArchiveObjectType type, Type nativeType,
        IArchiveReadManager.RestoreObjectDelegate readDelegate)
    {
        if (!registeredTypes.TryAdd(type, nativeType) || enumTypes.ContainsKey(type))
            throw new ArgumentException("Type is already registered");

        readDelegates[type] = readDelegate;
    }

    public void RegisterObjectType(ArchiveObjectType type, Type nativeType,
        IArchiveReadManager.AdvancedRestoreObjectDelegate readDelegate)
    {
        // Advanced callback can be registered for the same type as a normal one
        if (!registeredTypes.TryAdd(type, nativeType))
        {
            if (registeredTypes[type] != nativeType)
            {
                throw new ArgumentException(
                    "Registering an advanced reader for a type that is already registered with a different native backing type");
            }
        }

        readDelegatesAdvanced[type] = readDelegate;
    }

    public void RegisterBoxableValueType(ArchiveObjectType type, Type nativeType,
        IArchiveReadManager.CreateStructInstanceDelegate createInstanceDelegate)
    {
        if (!registeredTypes.TryAdd(type, nativeType) || enumTypes.ContainsKey(type))
            throw new ArgumentException("Type is already registered");

        readBoxedDelegates[type] = createInstanceDelegate;
    }

    public void RegisterEnumType(ArchiveObjectType type, ArchiveEnumType enumType, Type nativeType)
    {
        if (writeDelegates.ContainsKey(type) || readDelegates.ContainsKey(type) || registeredTypes.ContainsKey(type))
            throw new ArgumentException("Can't register conflicting enum type with another read / write type");

        enumTypes[type] = enumType;

        registeredTypes.Add(type, nativeType);
        registeredWriterTypes.Add(nativeType, type);
    }

    public void OnStartNewRead(ISArchiveReader reader)
    {
        // Just in case an exception interrupted the previous run, and someone calls a new read without finishing the
        // previous one
        loadedObjectReferences.Clear();
    }

    public void OnFinishRead(ISArchiveReader reader)
    {
        loadedObjectReferences.Clear();
    }

    public bool TryGetAlreadyReadObject(int referenceId, [NotNullWhen(true)] out object? obj)
    {
        return loadedObjectReferences.TryGetValue(referenceId, out obj);
    }

    public object ReadObject(ISArchiveReader reader, ArchiveObjectType type,
        ReadOnlySpan<ArchiveObjectType> extendedType, ushort version)
    {
        if (readDelegatesAdvanced.TryGetValue(type, out var advancedReadDelegate))
        {
            return advancedReadDelegate(reader,
                ResolveExtendedObjectType(type, extendedType, extendedType.Length, out _),
                version);
        }

        if (readDelegates.TryGetValue(type, out var readDelegate))
            return readDelegate(reader, version);

        if (readBoxedDelegates.TryGetValue(type, out var readBoxedDelegate))
        {
            var instance = readBoxedDelegate(reader, out var read, version);

            // Perform standard read if not already done
            if (!read)
            {
                instance.ReadFromArchive(reader, version);
            }

            return instance;
        }

        if (enumTypes.TryGetValue(type, out var enumType))
        {
            return ReadBoxedEnum(reader, type, enumType, version);
        }

        throw new FormatException($"Unsupported object type for reading: {type}");
    }

    public void ReadObjectToVariable<T>(ref T receiver, ISArchiveReader reader, ArchiveObjectType type, ushort version)
        where T : IArchiveReadableVariable
    {
        // TODO: support derived types
        if (receiver.ArchiveObjectType != type)
            throw new FormatException($"Object type mismatch: expected {receiver.ArchiveObjectType}, got {type}");

        receiver.ReadFromArchive(reader, version);
    }

    public bool RememberObject(object obj, int id)
    {
        return loadedObjectReferences.TryAdd(id, obj);
    }

    public Type? MapArchiveTypeToType(ArchiveObjectType type)
    {
        if (registeredTypes.TryGetValue(type, out var registeredType))
            return registeredType;

        // Return common types
        switch (type)
        {
            case ArchiveObjectType.Byte:
                return typeof(byte);
            case ArchiveObjectType.Bool:
                return typeof(bool);
            case ArchiveObjectType.Int16:
                return typeof(short);
            case ArchiveObjectType.Int32:
                return typeof(int);
            case ArchiveObjectType.Int64:
                return typeof(long);
            case ArchiveObjectType.UInt16:
                return typeof(ushort);
            case ArchiveObjectType.UInt32:
                return typeof(uint);
            case ArchiveObjectType.UInt64:
                return typeof(ulong);
            case ArchiveObjectType.Float:
                return typeof(float);
            case ArchiveObjectType.Double:
                return typeof(double);
            case ArchiveObjectType.String:
                return typeof(string);
            case ArchiveObjectType.VariableUint32:
                return typeof(uint);
            case ArchiveObjectType.Tuple:
                return typeof(ITuple);
            case ArchiveObjectType.ByteArray:
                return typeof(byte[]);

            case ArchiveObjectType.ExtendedList:
            case ArchiveObjectType.List:
                return typeof(List<>);

            // case ArchiveObjectType.Array:
            //     return typeof(Array);
            case ArchiveObjectType.ExtendedDictionary:
            case ArchiveObjectType.Dictionary:
                return typeof(Dictionary<,>);

            // Some of these may be problematic
            // case ArchiveObjectType.RawEnumerable:
            //     return typeof(IEnumerable);
            // case ArchiveObjectType.ReferenceTuple:
            //    return typeof(Tuple<,>);
        }

        return null;
    }
}
