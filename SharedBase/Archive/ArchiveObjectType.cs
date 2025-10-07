namespace SharedBase.Archive;

/// <summary>
///   Defines base types of archivable objects.
///   These may not be reordered or removed as that would invalidate all existing archives!
/// </summary>
public enum ArchiveObjectType : uint
{
    Invalid = 0,

    Null,
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

    // Used in testing
    TestObjectType1 = 4095,

    // Extended archive types for custom projects need to be defined after this value
    StartOfCustomTypes = 4096,

    LastValidObjectType = uint.MaxValue & 0xFFFFFF,
}
