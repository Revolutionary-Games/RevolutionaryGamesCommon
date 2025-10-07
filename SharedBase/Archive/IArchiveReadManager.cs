namespace SharedBase.Archive;

using System.Diagnostics.CodeAnalysis;

/// <summary>
///   Read-time variant of <see cref="IArchiveWriteManager"/>
/// </summary>
public interface IArchiveReadManager
{
    public delegate object RestoreObjectDelegate(ISArchiveReader reader, ushort version);

    public delegate void ReadStructDelegate<T>(ISArchiveReader reader, ArchiveObjectType type, ref T obj, ushort version);

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

    /// <summary>
    ///   Tries to get an already read object by its reference ID.
    /// </summary>
    /// <param name="referenceId">ID of the reference, which must be > 0</param>
    /// <param name="obj">Found object or null</param>
    /// <returns>True if an object was found</returns>
    public bool TryGetAlreadyReadObject(int referenceId, [NotNullWhen(true)] out object? obj);

    /// <summary>
    ///   Reads an object from the archive. Must be called just after the header is read.
    /// </summary>
    /// <param name="reader">Reader for performing the data read</param>
    /// <param name="type">Type of the object. Must have been previously registered.</param>
    /// <param name="version">Version of the object from the header info</param>
    /// <returns>The read object (throws on failure)</returns>
    public object ReadObject(ISArchiveReader reader, ArchiveObjectType type, ushort version);

    /// <summary>
    ///   Remembers an object for <see cref="TryGetAlreadyReadObject"/>
    /// </summary>
    /// <param name="obj">Object to remember</param>
    /// <param name="id">ID of the object which must be greater than 0</param>
    /// <returns>True if registered, false if already registered (this is usually a significant problem)</returns>
    public bool RememberObject(object obj, int id);
}
