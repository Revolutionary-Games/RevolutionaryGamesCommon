namespace SharedBase.Archive;

using System;

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

    public void RegisterObjectType(ArchiveObjectType type, Type nativeType, ArchiveObjectDelegate writeDelegate);

    /// <summary>
    ///   Returns true if the object is already referenced.
    /// </summary>
    /// <returns>True if referenced already by <see cref="MarkStartOfReferenceObject"/></returns>
    public bool IsReferencedAlready(object obj);

    /// <summary>
    ///   Gets the type a given type should be written as in an archive.
    /// </summary>
    /// <param name="type">C# type. Note that in some cases this will return a base type.</param>
    /// <returns>Type to use in the archive</returns>
    public ArchiveObjectType GetObjectWriteType(Type type);

    public bool ObjectChildTypeRequiresExtendedType(Type type);

    public void CalculateExtendedObjectType(ArchiveObjectType baseType, Type type,
        Span<ArchiveObjectType> extendedTypes, out int elementsWritten);

    public Type ResolveExtendedObjectType(ArchiveObjectType baseType, Span<ArchiveObjectType> extendedType,
        int elementCount);
}
