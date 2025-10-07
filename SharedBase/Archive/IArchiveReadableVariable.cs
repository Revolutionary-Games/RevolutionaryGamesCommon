namespace SharedBase.Archive;

/// <summary>
///   Marks a class as being able to read a new instance from an archive with a member method. Mainly used for structs.
/// </summary>
public interface IArchiveReadableVariable : IArchivable
{
    /// <summary>
    ///   Called to deserialize a "new" instance of this type from an archive. The current instance should be replaced with the new one.
    /// </summary>
    /// <param name="reader">Reader to read from</param>
    /// <param name="version">Version specified in the archive data, should be checked to match supported</param>
    public void ReadFromArchive(ISArchiveReader reader, ushort version);
}
