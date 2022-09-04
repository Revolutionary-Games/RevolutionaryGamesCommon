namespace ScriptsBase.Utilities;

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

public static class AssemblyInfoReader
{
    private static readonly Regex AssemblyVersionRegex = new(@"AssemblyVersion\(""([\d.]+)""\)");
    private static readonly Regex AssemblyExtraVersionRegex = new(@"AssemblyInformationalVersion\(""([^""]*)""\)");

    public static string ReadVersionFromAssemblyInfo(bool includeInformationalVersion = false,
        string file = "Properties/AssemblyInfo.cs")
    {
        string? version = null;
        string additionalVersion = string.Empty;

        foreach (var line in File.ReadLines(file, Encoding.UTF8))
        {
            var match = AssemblyVersionRegex.Match(line);

            if (match.Success)
            {
                version = match.Groups[1].Value;
                continue;
            }

            match = AssemblyExtraVersionRegex.Match(line);

            if (match.Success)
            {
                if (match.Groups[1].Length > 0)
                {
                    additionalVersion = match.Groups[1].Value;

                    // TODO: not the cleanest to combine this syntax check here, but this is how it was in the ruby
                    // version
                    if (!additionalVersion.StartsWith('-'))
                        throw new Exception("AssemblyInformationalVersion must start with a dash (or be empty)");
                }
            }
        }

        if (version == null)
            throw new Exception("Could not find AssemblyVersion");

        // Ensure that what we read conforms to teh C# assembly version requirements
        if (!Version.TryParse(version, out _))
        {
            throw new Exception($"Invalid version format for string: {version}");
        }

        if (includeInformationalVersion)
        {
            return $"{version}{additionalVersion}";
        }

        return version;
    }
}
