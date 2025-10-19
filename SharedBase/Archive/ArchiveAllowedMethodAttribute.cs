namespace SharedBase.Archive;

using System;

/// <summary>
///   Marks a method as allowed to be constructed into a delegate when loaded from an archive
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class ArchiveAllowedMethodAttribute : Attribute;
