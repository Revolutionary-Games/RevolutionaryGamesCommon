namespace SharedBase.Archive;

using System;

/// <summary>
///   Thrown when an object is null when loaded from an archive, but it is a mandatory object
/// </summary>
public class NullArchiveObjectException : FormatException
{
    public NullArchiveObjectException() : base("Read null object from an archive which was not expected")
    {
    }

    public NullArchiveObjectException(string fieldName) : base(
        $"Read null object from an archive which was not expected for field {fieldName}")
    {
    }
}
