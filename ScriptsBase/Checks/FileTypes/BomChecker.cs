namespace ScriptsBase.Checks.FileTypes;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class BomChecker : FileCheck
{
    public static readonly byte[] BomBytes = { 239, 187, 191 };

    private readonly Mode checkMode;

    public BomChecker(Mode checkMode, string firstHandledFileEnding, params string[] extraHandledFileEndings) : base(
        firstHandledFileEnding, extraHandledFileEndings)
    {
        this.checkMode = checkMode;
    }

    public enum Mode
    {
        Required,
        Disallowed,
    }

    public override async IAsyncEnumerable<string> Handle(string path)
    {
        var hasBom = await FileBeginsWithBom(path, CancellationToken.None);

        if (checkMode == Mode.Required && !hasBom)
        {
            yield return "File should begin with UTF-8 BOM";
        }
        else if (checkMode == Mode.Disallowed && hasBom)
        {
            yield return "File should NOT start with UTF-8 BOM";
        }
    }

    private static async Task<bool> FileBeginsWithBom(string path, CancellationToken cancellationToken)
    {
        await using var reader = File.OpenRead(path);

        var firstBytes = new byte[3];

        var read = await reader.ReadAsync(firstBytes, cancellationToken);

        if (read != firstBytes.Length)
        {
            throw new Exception("Failed to read the first 3 bytes of file");
        }

        return BomBytes.SequenceEqual(firstBytes);
    }
}
