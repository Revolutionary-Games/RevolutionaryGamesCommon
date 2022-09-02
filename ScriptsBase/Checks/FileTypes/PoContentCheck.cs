namespace ScriptsBase.Checks.FileTypes;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Karambolo.PO;

/// <summary>
///   Checks that the content and keys are fine in a PO file. Uses a parser.
///   Simple syntax check is in <see cref="PoFormatCheck"/>.
/// </summary>
public class PoContentCheck : FileCheck
{
    private static readonly HashSet<string> LocalizationUppercaseExceptions = new()
    {
        "Cancel",
    };

    private readonly POParser parser = LocalizationCheckBase.CreateParser();

    public PoContentCheck() : base(".po")
    {
    }

    public override IAsyncEnumerable<string> Handle(string path)
    {
        var isEnglish = path.EndsWith("en.po");

        // Sadly async parsing not supported, so we adapt here
        using var reader = File.OpenText(path);

        return ParseAndHandle(reader, isEnglish).ToAsyncEnumerable();
    }

    private IEnumerable<string> ParseAndHandle(StreamReader reader, bool isEnglish)
    {
        var result = parser.Parse(reader);

        if (!result.Success)
        {
            yield return "PO parsing failed:";

            foreach (var diagnostic in result.Diagnostics)
            {
                yield return diagnostic.ToString();
            }

            yield break;
        }

        var catalog = result.Catalog;

        string? previousTranslation = null;

        foreach (var entry in catalog)
        {
            bool hasSomething = false;
            bool hasUnderscore = false;

            var id = entry.Key.Id;

            if (string.IsNullOrWhiteSpace(id))
            {
                yield return $"Translation key is empty. Empty key is after key: {previousTranslation}";
            }

            foreach (var translation in entry)
            {
                if (!string.IsNullOrEmpty(translation))
                {
                    if (translation.Contains('_'))
                        hasUnderscore = true;

                    hasSomething = true;
                    break;
                }
            }

            if (!hasSomething && isEnglish)
            {
                yield return $"Translation text for {id} is blank";
            }

            if (hasUnderscore && string.Join('\n', entry) == id)
            {
                yield return $"Translation text for {id} is the same as the key";
            }

            if (id.ToUpperInvariant() != id && !LocalizationUppercaseExceptions.Contains(id))
            {
                yield return $"Translation key {id} has non-uppercase characters";
            }

            if (id.Contains(' '))
            {
                yield return $"Translation key '{id}' has spaces in it";
            }

            previousTranslation = id;
        }
    }
}
