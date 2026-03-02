namespace ScriptsBase.Checks.FileTypes;

using System.Collections.Generic;

/// <summary>
///   Disallows file names that differ just by case. Ensures projects work fine on platforms with case-insensitive
///   file systems.
/// </summary>
public class CaseInsensitiveFileCheck : FileCheck
{
    private readonly HashSet<string> seenLowerCaseNames = new();

    public CaseInsensitiveFileCheck() : base(string.Empty)
    {
        HandlesBigBinaryFiles = true;
    }

    public override async IAsyncEnumerable<string> Handle(string path)
    {
        var lowerCase = path.ToLowerInvariant();

        if (path.Contains("textures"))
            _ = 1 + 2;

        if (!seenLowerCaseNames.Add(lowerCase))
        {
            yield return $"File name {lowerCase} has been seen already. " +
                $"This is most likely caused by files that differ only by case.\n" +
                "On systems with case-insensitive file systems, this will cause major issues, " +
                "so please fix the naming of the file(s)!";
        }
    }
}
