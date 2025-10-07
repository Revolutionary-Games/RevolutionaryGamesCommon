namespace SharedBase.Archive;

using System;
using System.Collections.Generic;

/// <summary>
///   Default implementation of the archive manager that handles both reading and writing.
/// </summary>
public class DefaultArchiveManager : IArchiveWriteManager, IArchiveReadManager
{
    // Registered custom types
    private readonly Dictionary<ArchiveObjectType, IArchiveWriteManager.ArchiveObjectDelegate> writeDelegates = new();
    private readonly Dictionary<ArchiveObjectType, IArchiveReadManager.RestoreObjectDelegate> readDelegates = new();

    // TODO: determine how to support this well
    // private readonly Dictionary<ArchiveObjectType, IArchiveReadManager.ReadStructDelegate<object>> structReadDelegates = new();

    // Object reference handling
    private readonly Dictionary<object, long> objectIdPositions = new();
    private readonly Dictionary<object, int> objectIds = new();

    /// <summary>
    ///   Used on a load to know what objects are loaded
    /// </summary>
    private readonly Dictionary<long, object> loadedObjectReferences = new();

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
        if (objectIdPositions.TryGetValue(obj, out _))
        {
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

            // But write the ID that will be used instead of this object
            writer.Write(id);

            return true;
        }

        // Writing a new object, just store the place for the ID, it will only be really used if this object is needed
        // a second time
        objectIdPositions.Add(obj, writer.GetPosition());

        return false;
    }

    public void RegisterObjectType(ArchiveObjectType type, bool canBeReference,
        IArchiveWriteManager.ArchiveObjectDelegate writeDelegate)
    {
        // TODO: does this need to know if this can be a reference?

        writeDelegates[type] = writeDelegate;
    }

    public void RegisterObjectType(ArchiveObjectType type, IArchiveReadManager.RestoreObjectDelegate readDelegate)
    {
        readDelegates[type] = readDelegate;
    }

    public void RegisterValueType<T>(ArchiveObjectType type, IArchiveReadManager.ReadStructDelegate<T> readDelegate)
    {
        throw new System.NotImplementedException();
    }

    public void OnStartNewRead(ISArchiveWriter writer)
    {
        throw new System.NotImplementedException();
    }

    public void OnFinishRead(ISArchiveWriter writer)
    {
        loadedObjectReferences.Clear();
    }
}
