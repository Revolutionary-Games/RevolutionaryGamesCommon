namespace ScriptsBase.Utilities;

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;

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

        // Ensure that what we read conforms to the C# assembly version requirements
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

    public static string ReadVersionFromCsproj(string csprojFile)
    {
        var csproj = XElement.Load(csprojFile);

        var version = csproj.XPathSelectElement("PropertyGroup//Version");

        if (version == null)
            throw new ArgumentException("Could not find version in the file");

        var versionString = version.Value;

        if (!Version.TryParse(versionString, out _))
        {
            throw new Exception($"Invalid version format for string: {version}");
        }

        return versionString;
    }

    public static (string Version, string Authors, string AssemblyTitle, string Copyright, string Description)
        ReadAllProjectVersionMetadata(string csprojFile)
    {
        var csproj = XElement.Load(csprojFile);

        var version = csproj.XPathSelectElement("PropertyGroup//Version") ??
            throw new ArgumentException("Could not find Version");
        var authors = csproj.XPathSelectElement("PropertyGroup//Authors") ??
            throw new ArgumentException("Could not find Authors");
        var assemblyTitle = csproj.XPathSelectElement("PropertyGroup//AssemblyTitle") ??
            throw new ArgumentException("Could not find AssemblyTitle");
        var copyright = csproj.XPathSelectElement("PropertyGroup//Copyright") ??
            throw new ArgumentException("Could not find Copyright");
        var description = csproj.XPathSelectElement("PropertyGroup//Description") ??
            throw new ArgumentException("Could not find Description");

        return (version.Value, authors.Value, assemblyTitle.Value, copyright.Value, description.Value);
    }
}
