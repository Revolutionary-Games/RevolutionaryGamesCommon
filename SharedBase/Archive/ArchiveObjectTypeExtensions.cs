namespace SharedBase.Archive;

using System;

public static class ArchiveObjectTypeExtensions
{
    public static bool IsExtendedType(this ArchiveObjectType type)
    {
        // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
        return (type & ArchiveObjectType.ExtendedTypeFlag) != 0;
    }

    public static ArchiveObjectType RemoveExtendedFlag(this ArchiveObjectType type)
    {
        if (!type.IsExtendedType())
            throw new ArgumentException("Can only remove the extended flag if it is there");

        // ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
        return type & ~ArchiveObjectType.ExtendedTypeFlag;
    }
}
