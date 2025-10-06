namespace SharedBase.Archive;

/// <summary>
///   Read-time variant of <see cref="IArchiveWriteManager"/>
/// </summary>
public interface IArchiveReadManager
{
    public delegate object RestoreObjectDelegate(ISArchiveReader reader, ArchiveObjectType type);

    public delegate void ReadStructDelegate<T>(ISArchiveReader reader, ArchiveObjectType type, ref T obj);

    public static void RegisterDefaultObjectReaders(IArchiveReadManager manager)
    {
        // TODO: list, dictionary, tuple etc. readers
    }

    /// <summary>
    ///   Register a custom object type to be read from an archive
    /// </summary>
    /// <param name="type">Type the factory handles</param>
    /// <param name="readDelegate">Factory method that is invoked to create the object on read</param>
    public void RegisterObjectType(ArchiveObjectType type, RestoreObjectDelegate readDelegate);

    public void RegisterValueType<T>(ArchiveObjectType type, ReadStructDelegate<T> readDelegate);

    public void OnStartNewRead(ISArchiveWriter writer);
    public void OnFinishRead(ISArchiveWriter writer);
}
