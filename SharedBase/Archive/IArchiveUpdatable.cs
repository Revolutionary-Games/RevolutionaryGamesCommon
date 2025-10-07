namespace SharedBase.Archive;

/// <summary>
///   Marks an object that can have its properties read from an archive.
///   This is in contrast to <see cref="IArchivable"/>, which marks objects read entirely from an archive.
/// </summary>
public interface IArchiveUpdatable
{
    public ushort CurrentArchiveVersion { get; }
    public ArchiveObjectType ArchiveObjectType { get; }

    public void WritePropertiesToArchive(ISArchiveWriter writer);

    public void ReadPropertiesFromArchive(ISArchiveReader reader, ushort version);
}
