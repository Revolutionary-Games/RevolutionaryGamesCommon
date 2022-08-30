namespace ScriptsBase.Utilities;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

/// <summary>
///   Used to detect a subset of all files to run checks on
/// </summary>
public static class OnlyChangedFileDetector
{
    public const string ONLY_FILE_LIST = "files_to_check.txt";

    /// <summary>
    ///   Detects if there is a file telling which files to check
    /// </summary>
    /// <returns>The list of files to check or null</returns>
    public static IEnumerable<string>? DetectOnlySomeFilesConfiguredForChecking()
    {
        if (!File.Exists(ONLY_FILE_LIST))
            return null;

        var result = new HashSet<string>();

        foreach (var line in File.ReadLines(ONLY_FILE_LIST, Encoding.UTF8))
        {
            var processed = line.Trim().Replace("./", "").TrimStart('/');

            if (processed.Length > 0)
                result.Add(processed);
        }

        if (result.Count < 1)
            return null;

        return result;
    }

    public static bool IncludesChangesToFileType(string suffix, IEnumerable<string>? files)
    {
        if (files == null)
            return true;

        return files.Any(f => f.EndsWith(suffix));
    }
}
