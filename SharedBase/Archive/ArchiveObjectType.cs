namespace SharedBase.Archive;

/// <summary>
///   Defines base types of archivable objects.
///   These may not be reordered or removed as that would invalidate all existing archives!
/// </summary>
public enum ArchiveObjectType : uint
{
    Invalid = 0,

    Byte,
    Int16,
    Int32,
    Int64,
    Uint16,
    Uint32,
    Uint64,
    Float,
    Double,

    String,
    VariableUint32,

    // More complex types
    Tuple,
    ByteArray,
    Array,
    Dictionary,

    StartOfCustomTypes = 4096,

    LastValidObjectType = uint.MaxValue & 0xFFFFFF,
}
