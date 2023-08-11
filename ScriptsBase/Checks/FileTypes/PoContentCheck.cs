namespace ScriptsBase.Checks.FileTypes;

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
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

    public override async IAsyncEnumerable<string> Handle(string path)
    {
        var isEnglish = path.EndsWith("en.po");

        // Sadly async parsing not supported, so we adapt here
        var parseTask = new Task<POParseResult>(() =>
        {
            using var reader = File.OpenText(path);

            return parser.Parse(reader);
        });
        parseTask.Start();

        var result = await parseTask;

        if (!result.Success)
        {
            yield return "PO parsing failed:";

            foreach (var diagnostic in result.Diagnostics)
            {
                yield return diagnostic.ToString() ?? "unknown error";
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

            string? lastText = null;

            foreach (var translation in entry)
            {
                if (!string.IsNullOrEmpty(translation))
                {
                    if (translation.Contains('_'))
                        hasUnderscore = true;

                    lastText = translation;
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

            if (isEnglish && lastText != null && lastText.TrimEnd() != lastText)
            {
                yield return $"Translation text for {id} ends with whitespace, which is not allowed. " +
                    "Instead insert spacing or padding with code or Control Nodes.";
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
