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
    Char,
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
    Set,

    Delegate,

    /// <summary>
    ///   Used for storing generic containers that want to contain anything
    /// </summary>
    Object,

    // Only used in testing
    TestObjectType1 = 4094,
    TestObjectType2 = 4095,

    // Extended archive types for custom projects need to be defined after this value
    StartOfCustomTypes = 4096,

    LastValidObjectType = uint.MaxValue & 0x7FFFFF,

    /// <summary>
    ///   If this bit is set, then the object type is actually an extended type, meaning that after the object header,
    ///   there's a special section for the extended type.
    /// </summary>
    ExtendedTypeFlag = 1 << 23,

    /// <summary>
    ///   These types are stored with 24 bits only, so these are the valid bits
    /// </summary>
    ValidBits = uint.MaxValue & 0xFFFFFF,

    // Easy access values to some commonly extended types
    ExtendedList = List | ExtendedTypeFlag,
    ExtendedDictionary = Dictionary | ExtendedTypeFlag,

    // TODO: should arrays use extended types? (it kind of makes sense if it is a more limited type)
    // ExtendedArray = Array | ExtendedTypeFlag,

    ExtendedTuple = Tuple | ExtendedTypeFlag,
    ExtendedReferenceTuple = ReferenceTuple | ExtendedTypeFlag,

    ExtendedSet = Set | ExtendedTypeFlag,
}
