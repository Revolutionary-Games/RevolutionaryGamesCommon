namespace ScriptsBase.Translation;

/// <summary>
///   A single extracted translation that has not been de-duplicated yet
/// </summary>
public class ExtractedTranslation
{
    public ExtractedTranslation(string translationKey, string sourceFile, int lineNumber)
    {
        TranslationKey = translationKey;
        SourceFile = sourceFile;
        SourceLocation = $"{sourceFile}:{lineNumber}";
    }

    public string TranslationKey { get; }

    public string SourceFile { get; }
    public string SourceLocation { get; }

    public override string ToString()
    {
        return $"{TranslationKey} at {SourceLocation}";
    }
}
