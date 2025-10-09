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
    Bool,
    Int16,
    Int32,
    Int64,
    UInt16,
    UInt32,
    UInt64,
    Float,
    Double,

    String,
    VariableUint32,

    // More complex types
    Tuple,
    ReferenceTuple,
    ByteArray,
    List,
    Array,
    Dictionary,
    RawEnumerable,

    // Only used in testing
    TestObjectType1 = 4094,
    TestObjectType2 = 4095,

    // Extended archive types for custom projects need to be defined after this value
    StartOfCustomTypes = 4096,

    LastValidObjectType = uint.MaxValue & 0xFFFFFF,
}
