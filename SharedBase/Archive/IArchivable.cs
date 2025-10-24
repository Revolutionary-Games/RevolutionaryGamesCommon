namespace SharedBase.Archive;

/// <summary>
///   Interface for archivable objects
/// </summary>
public interface IArchivable
{
    /// <summary>
    ///   Archive version of this object.
    ///   Should be started at 1, and incremented for each new version where data is different.
    ///   Deserializing objects should, if at all possible, handle older versions.
    /// </summary>
    public ushort CurrentArchiveVersion { get; }

    public ArchiveObjectType ArchiveObjectType { get; }

    /// <summary>
    ///   If true, this object can be referenced in an archive. So if this is multiple times in the object tree, then
    ///   only one copy is saved. Note that if descendants can refer to this object,
    ///   then <see cref="ISArchiveReader.ReportObjectConstructorDone"/>
    /// </summary>
    public bool CanBeReferencedInArchive { get; }

    /// <summary>
    ///   Called when this should be written to an archive.
    /// </summary>
    /// <param name="writer">Writer to use to write all fields of this object</param>
    public void WriteToArchive(ISArchiveWriter writer);
}
