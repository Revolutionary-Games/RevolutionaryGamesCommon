namespace SharedBase.Archive;

/// <summary>
///   Interface for archivable objects
/// </summary>
public interface IArchivable
{
    public ushort CurrentArchiveVersion { get; }
    public ArchiveObjectType ArchiveObjectType { get; }
    public bool CanBeReferencedInArchive { get; }

    public void WriteToArchive(ISArchiveWriter writer);
}
