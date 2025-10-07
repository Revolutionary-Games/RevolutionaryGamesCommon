namespace SharedBase.Archive;

using System;

/// <summary>
///   Thrown when an object type encounters an archive version it cannot handle
/// </summary>
public class InvalidArchiveVersionException : FormatException
{
    public InvalidArchiveVersionException(ushort version, ushort wanted) : base(
        $"Invalid archive object version {version}, wanted {wanted}")
    {
    }
}
