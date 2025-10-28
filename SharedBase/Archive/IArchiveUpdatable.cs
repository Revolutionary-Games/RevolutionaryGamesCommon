namespace SharedBase.Archive;

/// <summary>
///   Marks an object that can have its properties read from an archive.
///   This is in contrast to <see cref="IArchivable"/>, which marks objects read entirely from an archive.
/// </summary>
public interface IArchiveUpdatable
{
    public ushort CurrentArchiveVersion { get; }
    public ArchiveObjectType ArchiveObjectType { get; }

    /// <summary>
    ///   If true, then this value can be referenced in the archive in special cases. Normally updatable values cannot
    ///   be referenced at all, but that is necessary in some situations like delegate targets.
    /// </summary>
    public bool CanBeSpecialReference => false;

    public void WritePropertiesToArchive(ISArchiveWriter writer);

    public void ReadPropertiesFromArchive(ISArchiveReader reader, ushort version);
}
