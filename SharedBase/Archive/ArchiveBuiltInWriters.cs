namespace SharedBase.Archive;

using System;
using System.Collections;

public static class ArchiveBuiltInWriters
{
    /// <summary>
    ///   Specialty set writer, which is needed as there's no base interface for ISet without the generic type.
    /// </summary>
    public static void WriteUnknownSet(ISArchiveWriter writer, ArchiveObjectType type, object obj)
    {
        if (type != ArchiveObjectType.Set && type != ArchiveObjectType.ExtendedSet)
            throw new NotSupportedException("This method is only for writing sets");

        var enumerable = (IEnumerable)obj;

        // SArchiveWriterBase has an implementation when the type is known to be a ISet<T>

        // TODO: should this check to find some kind of ISet interface specialization?

        // Use reflection to get the object type inside the set

        var objType = obj.GetType();
        var childTypes = objType.GetGenericArguments();

        if (childTypes.Length != 1)
            throw new FormatException("Set must have a single generic type");

        // Set probably needs to be extended always to know the actual type to construct as the set might be empty
        bool extended = true;

        // var childType = childTypes[0];
        // bool extended = writer.WriteManager.ObjectChildTypeRequiresExtendedType(childType);

        // Make sure the extended flag matches the type
        if (extended)
        {
            type = ArchiveObjectType.ExtendedSet;
        }
        else
        {
            // It should be safe to unset the flag if it was set before
            type = ArchiveObjectType.Set;
        }

        writer.WriteObjectHeader(type, false, false, false, extended, SArchiveWriterBase.COLLECTIONS_VERSION);

        if (extended)
            writer.HandleExtendedTypeWrite(type, objType);

        // We need to use reflection to get the size of the set
        var size = (int)(objType.GetProperty("Count")?.GetValue(obj) ??
            throw new Exception("Cannot get size of set through reflection"));

        writer.WriteVariableLengthField32((uint)size);

        writer.Write((uint)writer.WriteManager.GetObjectWriteType(objType));

        // This does not write optimised sets
        writer.Write((byte)0);

        // Causes an enumerator, but this is the only interface we can use
        foreach (var item in enumerable)
        {
            writer.WriteAnyRegisteredValueAsObject(item);
        }
    }
}
