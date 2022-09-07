namespace ScriptsBase.Models;

using System.Collections.Generic;
using System.IO;
using System.Linq;

public class FileToPackage
{
    private IReadOnlyCollection<PackagePlatform>? onlyForPlatforms;

    public FileToPackage(string original, string newName, PackagePlatform? onlyForPlatform = null) : this(original,
        newName, onlyForPlatform != null ? new[] { onlyForPlatform.Value } : null)
    {
    }

    public FileToPackage(string original) : this(original, Path.GetFileName(original))
    {
    }

    public FileToPackage(string original, string newName, IEnumerable<PackagePlatform>? onlyForPlatforms)
    {
        OriginalFile = original;
        PackagePathAndName = newName;

        if (onlyForPlatforms != null)
            this.onlyForPlatforms = onlyForPlatforms.ToList();
    }

    public string OriginalFile { get; }

    public string PackagePathAndName { get; }

    public bool IsForPlatform(PackagePlatform platform)
    {
        if (onlyForPlatforms == null)
            return true;

        return onlyForPlatforms.Contains(platform);
    }
}
