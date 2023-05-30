namespace ScriptsBase.Checks.FileTypes;

using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
///   Disallows the specified file types existing
/// </summary>
public class DisallowedFileType : FileCheck
{
    public DisallowedFileType(string firstHandledFileEnding, params string[] extraHandledFileEndings) : base(
        firstHandledFileEnding, extraHandledFileEndings)
    {
    }

    public Dictionary<string, string> ExtraErrorMessages { get; } = new();

    public override IAsyncEnumerable<string> Handle(string path)
    {
        var extension = Path.GetExtension(path);

        if (string.IsNullOrEmpty(extension))
            extension = Path.GetFileName(path);

        return new[] { GetErrorMessage(extension) }.ToAsyncEnumerable();
    }

    private string GetErrorMessage(string extension)
    {
        var baseError = $"Files of type {extension} should not exist";

        if (ExtraErrorMessages.TryGetValue(extension, out var extra))
        {
            return $"{baseError}. {extra}";
        }

        return baseError;
    }
}
