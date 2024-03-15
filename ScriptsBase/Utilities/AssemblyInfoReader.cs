namespace ScriptsBase.Utilities;

using System;
using System.Xml.Linq;
using System.Xml.XPath;

public static class AssemblyInfoReader
{
    private const string AssemblyVersionName = "PropertyGroup//Version";
    private const string AssemblyVersionExtraName = "PropertyGroup//InformationalVersion";

    public static string ReadVersionFromCsproj(string csprojFile, bool includeInformationalVersion = false)
    {
        var csproj = XElement.Load(csprojFile);

        var version = csproj.XPathSelectElement(AssemblyVersionName);

        if (version == null)
            throw new ArgumentException("Could not find version in the file");

        string additionalVersion = string.Empty;

        if (includeInformationalVersion)
        {
            var additionalVersionElement = csproj.XPathSelectElement(AssemblyVersionExtraName);

            if (additionalVersionElement != null)
                additionalVersion = additionalVersionElement.Value;

            if (additionalVersion.Length > 0 && !additionalVersion.StartsWith('-'))
            {
                throw new AggregateException("Additional version in file should start with a dash");
            }
        }

        var versionString = version.Value;

        // Ensure that what we read conforms to the C# assembly version requirements
        if (!Version.TryParse(versionString, out _))
        {
            throw new Exception($"Invalid version format for string: {version}");
        }

        if (includeInformationalVersion)
        {
            return $"{versionString}{additionalVersion}";
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
