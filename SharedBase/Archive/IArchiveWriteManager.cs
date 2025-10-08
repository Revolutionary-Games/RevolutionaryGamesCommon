namespace SharedBase.Archive;

/// <summary>
///   Manages high-level operations and special custom type handling for archives
/// </summary>
public interface IArchiveWriteManager
{
    public delegate void ArchiveObjectDelegate(ISArchiveWriter writer, ArchiveObjectType type, object obj);

    public void OnStartNewWrite(ISArchiveWriter writer);

    public void OnFinishWrite(ISArchiveWriter writer);

    /// <summary>
    ///   Call when an object that can be referenced multiple times in an archive is about to be written.
    ///   This saves the current writing offset to be used later for referencing the object.
    /// </summary>
    /// <param name="writer">Used writer</param>
    /// <param name="obj">Object</param>
    /// <returns>
    ///   True if the object is already written and the writing method should not write the object properties again
    /// </returns>
    public bool MarkStartOfReferenceObject(ISArchiveWriter writer, object obj);

    public void RegisterObjectType(ArchiveObjectType type, bool canBeReference, ArchiveObjectDelegate writeDelegate);

    /// <summary>
    ///   Returns true if the object is already referenced.
    /// </summary>
    /// <returns>True if referenced already by <see cref="MarkStartOfReferenceObject"/></returns>
    public bool IsReferencedAlready(object obj);
}
