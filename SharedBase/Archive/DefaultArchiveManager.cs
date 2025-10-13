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
    // Registered custom types
    // TODO: do we need these write delegates?
    private readonly Dictionary<ArchiveObjectType, IArchiveWriteManager.ArchiveObjectDelegate> writeDelegates = new();
    private readonly Dictionary<ArchiveObjectType, IArchiveReadManager.RestoreObjectDelegate> readDelegates = new();

    private readonly Dictionary<ArchiveObjectType, IArchiveReadManager.CreateStructInstanceDelegate>
        readBoxedDelegates = new();

    private readonly Dictionary<ArchiveObjectType, Type> registeredTypes = new();
    private readonly Dictionary<Type, ArchiveObjectType> registeredWriterTypes = new();

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
                // TODO: extended types for tuples?
                if (baseType.IsValueType)
                    return ArchiveObjectType.Tuple;

                return ArchiveObjectType.ReferenceTuple;
            }
        }

        throw new ArgumentException($"Type is not registered for archive writing: {type}");
    }

    public bool ObjectChildTypeRequiresExtendedType(Type type)
    {
        if (type.IsGenericType)
            return true;

        // TODO: should tuples use extended types?
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

        // Then write the child element types
        foreach (var childType in type.GetGenericArguments())
        {
            var typeToWrite = GetObjectWriteType(childType);
            extendedTypes[elementsWritten] = typeToWrite;
            elementsWritten += 1;

            // If the child type is extended, recursively write out its types as well
            if (typeToWrite.IsExtendedType())
            {
                CalculateExtendedObjectType(typeToWrite, childType,
                    extendedTypes.Slice(elementsWritten, extendedTypes.Length - elementsWritten), out var newElements);
                elementsWritten += newElements;
            }
        }

        // Check we didn't write accidentally too much
        if (elementsWritten > ISArchiveWriter.ReasonableMaxExtendedType)
            throw new ArgumentException($"Extended type length too long: {elementsWritten}");
    }

    public Type ResolveExtendedObjectType(ArchiveObjectType baseType, Span<ArchiveObjectType> extendedType,
        int elementCount)
    {
        if (!baseType.IsExtendedType())
            throw new ArgumentException("Base type must be an extended type");

        if (elementCount < 1)
            throw new ArgumentException("Cannot decode empty extended type");

        if (extendedType.Length < elementCount)
            throw new ArgumentException("Span length mismatch");

        var nextType = extendedType[0];

        var typeToCheckAgainst = nextType;

        if (typeToCheckAgainst.IsExtendedType())
            typeToCheckAgainst = typeToCheckAgainst.RemoveExtendedFlag();

        if (baseType.RemoveExtendedFlag() != typeToCheckAgainst)
        {
            throw new FormatException(
                $"Extended type mismatch, expected to see: {baseType} but read from archive: {nextType}");
        }

        var type = MapArchiveTypeToType(nextType);

        if (type == null)
            throw new FormatException($"Unknown extended type base: {nextType}");

        if (!type.IsGenericType)
            throw new Exception($"Extended type base is not a generic type: {type}");

        // var genericBase = type.GetGenericTypeDefinition();
        var types = new Type[type.GenericTypeArguments.Length];

        for (int i = 0; i < types.Length; ++i)
        {
            int elementIndex = i + 1;
            if (elementIndex >= type.GenericTypeArguments.Length)
                throw new FormatException($"Ran out of extended type elements for {type} at index: {i}");

            var nextElement = extendedType[elementIndex];

            if (nextElement.IsExtendedType())
            {
                types[i] = ResolveExtendedObjectType(nextElement,
                    extendedType.Slice(elementIndex, extendedType.Length - elementIndex), elementCount - elementIndex);
            }
            else
            {
                types[i] = MapArchiveTypeToType(nextElement) ??
                    throw new FormatException($"Unknown extended type part ({i}): {nextElement}");
            }
        }

        return type.MakeGenericType(types);
    }

    public void RegisterObjectType(ArchiveObjectType type, Type nativeType,
        IArchiveWriteManager.ArchiveObjectDelegate writeDelegate)
    {
        if (!registeredWriterTypes.TryAdd(nativeType, type))
            throw new ArgumentException("Type is already registered");

        writeDelegates[type] = writeDelegate;
    }

    public void RegisterObjectType(ArchiveObjectType type, Type nativeType,
        IArchiveReadManager.RestoreObjectDelegate readDelegate)
    {
        if (!registeredTypes.TryAdd(type, nativeType))
            throw new ArgumentException("Type is already registered");

        readDelegates[type] = readDelegate;
    }

    public void RegisterBoxableValueType(ArchiveObjectType type, Type nativeType,
        IArchiveReadManager.CreateStructInstanceDelegate createInstanceDelegate)
    {
        if (!registeredTypes.TryAdd(type, nativeType))
            throw new ArgumentException("Type is already registered");

        readBoxedDelegates[type] = createInstanceDelegate;
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
