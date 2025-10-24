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

    /// <summary>
    ///   Register a new custom type for writing in nested situations. Note that the callback must write an object
    ///   header first!
    /// </summary>
    /// <remarks>
    ///   <para>
    ///     The recommended definition for the write method is as follows:
    ///     <c>
    ///       public static void WriteToArchive(ISArchiveWriter writer, ArchiveObjectType type, object obj)
    ///       {
    ///           if (type != (ArchiveObjectType)ThriveArchiveObjectType.ReproductionOrganelleData)
    ///               throw new NotSupportedException();
    ///           writer.WriteObject((ReproductionOrganelleData)obj);
    ///       }
    ///     </c>
    ///   </para>
    /// </remarks>
    /// <param name="type">Archive type</param>
    /// <param name="nativeType">C# type to register</param>
    /// <param name="writeDelegate">The delegate that is called to do the actual writing</param>
    public void RegisterObjectType(ArchiveObjectType type, Type nativeType, ArchiveObjectDelegate writeDelegate);

    /// <summary>
    ///   Registers a custom enum type.
    /// </summary>
    /// <param name="type">Type in the archive</param>
    /// <param name="enumType">Size of the necessary data for the enum</param>
    /// <param name="nativeType">The native C# side type of the enum (<c>typeof(T)</c>)</param>
    public void RegisterEnumType(ArchiveObjectType type, ArchiveEnumType enumType, Type nativeType);

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

    public bool TryGetObjectWriteType(Type type, out ArchiveObjectType archiveType);

    public bool ObjectChildTypeRequiresExtendedType(Type type);

    public bool WriteCustomEnumIfPossible<T>(ISArchiveWriter writer, T value);

    /// <summary>
    ///   If a writer is registered for the given type, then it will be used to write the value.
    /// </summary>
    /// <returns>True if written</returns>
    public bool WriteIfRegisteredObjectType<T>(ISArchiveWriter writer, T value);

    /// <summary>
    ///   Calculates the extended type data for a given type. Note that the calculated data can include special values
    ///   that are not by themselves valid <see cref="ArchiveObjectType"/> values."/>
    /// </summary>
    /// <param name="baseType">Base type to get extended data for</param>
    /// <param name="type">The C# type to inspect</param>
    /// <param name="extendedTypes">Return value</param>
    /// <param name="elementsWritten">Specifies how much data was actually written</param>
    public void CalculateExtendedObjectType(ArchiveObjectType baseType, Type type,
        Span<ArchiveObjectType> extendedTypes, out int elementsWritten);

    /// <summary>
    ///   Clear state if a write operation needs to be abandoned.
    /// </summary>
    public void Clear();
}
