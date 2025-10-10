namespace SharedBase.Archive;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

/// <summary>
///   Read-time variant of <see cref="IArchiveWriteManager"/>
/// </summary>
public interface IArchiveReadManager
{
    public delegate object RestoreObjectDelegate(ISArchiveReader reader, ushort version);

    public delegate IArchiveReadableVariable CreateStructInstanceDelegate(ISArchiveReader reader,
        out bool performedCustomRead,
        ushort version);

    public static void RegisterDefaultObjectReaders(IArchiveReadManager manager)
    {
        // Reference tuple
        manager.RegisterObjectType(ArchiveObjectType.ReferenceTuple, typeof(ITuple),
            ArchiveBuiltInReaders.ReadReferenceTuple);

        // List reader
        manager.RegisterObjectType(ArchiveObjectType.List, typeof(IList<>),
            ArchiveBuiltInReaders.ReadList);

        // Array reader
        manager.RegisterObjectType(ArchiveObjectType.Array, typeof(Array),
            ArchiveBuiltInReaders.ReadArray);

        // Dictionaries
        manager.RegisterObjectType(ArchiveObjectType.Dictionary, typeof(Dictionary<,>),
            ArchiveBuiltInReaders.ReadDictionary);
    }

    /// <summary>
    ///   Register a custom object type to be read from an archive
    /// </summary>
    /// <param name="type">Type the factory handles</param>
    /// <param name="nativeType">Type of the C# object that matches the type</param>
    /// <param name="readDelegate">Factory method that is invoked to create the object on read</param>
    public void RegisterObjectType(ArchiveObjectType type, Type nativeType, RestoreObjectDelegate readDelegate);

    /// <summary>
    ///   Registers a struct type that needs to be read to generic variables where <see cref="ReadObjectToVariable"/>
    ///   cannot be used.
    ///   This approach needs a constructor that makes boxed instances.
    /// </summary>
    /// <param name="type">Type of the object</param>
    /// <param name="nativeType">Native type in C#</param>
    /// <param name="createInstanceDelegate">
    ///   Factory for making basically plain instances, though optionally, these can do a custom read if needed for
    ///   a constructor.
    /// </param>
    public void RegisterBoxableValueType(ArchiveObjectType type, Type nativeType,
        CreateStructInstanceDelegate createInstanceDelegate);

    public void OnStartNewRead(ISArchiveReader reader);
    public void OnFinishRead(ISArchiveReader reader);

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

    public void ReadObjectToVariable<T>(ref T receiver, ISArchiveReader reader, ArchiveObjectType type, ushort version)
        where T : IArchiveReadableVariable;

    /// <summary>
    ///   Remembers an object for <see cref="TryGetAlreadyReadObject"/>
    /// </summary>
    /// <param name="obj">Object to remember</param>
    /// <param name="id">ID of the object which must be greater than 0</param>
    /// <returns>True if registered, false if already registered (this is usually a significant problem)</returns>
    public bool RememberObject(object obj, int id);

    /// <summary>
    ///   Maps an archive type to a C# type. Custom types need registering with <see cref="RegisterObjectType"/>.
    ///   Supports a fixed set of built-in and basic types.
    /// </summary>
    /// <param name="type">Type to find a native C# match for</param>
    /// <returns>The known matching type or null</returns>
    public Type? MapArchiveTypeToType(ArchiveObjectType type);
}
