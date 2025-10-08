namespace SharedBase.Archive;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

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

        return false;
    }

    public bool IsReferencedAlready(object obj)
    {
        return objectIds.ContainsKey(obj);
    }

    public void RegisterObjectType(ArchiveObjectType type, bool canBeReference,
        IArchiveWriteManager.ArchiveObjectDelegate writeDelegate)
    {
        // TODO: does this need to know if this can be a reference?

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
    }

    public void OnFinishRead(ISArchiveReader reader)
    {
        loadedObjectReferences.Clear();
    }

    public bool TryGetAlreadyReadObject(int referenceId, [NotNullWhen(true)] out object? obj)
    {
        return loadedObjectReferences.TryGetValue(referenceId, out obj);
    }

    public object ReadObject(ISArchiveReader reader, ArchiveObjectType type, ushort version)
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
        return registeredTypes.GetValueOrDefault(type);
    }
}
