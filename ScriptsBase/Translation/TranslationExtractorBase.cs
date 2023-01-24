namespace ScriptsBase.Translation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

/// <summary>
///   Base class for translation extraction tools for different file types
/// </summary>
public abstract class TranslationExtractorBase
{
    /// <summary>
    ///   Sets up an extractor base to work for specific types of files
    /// </summary>
    /// <param name="handledFileExtensions">The types of files this extractor will run on</param>
    protected TranslationExtractorBase(params string[] handledFileExtensions)
    {
        HandledFileEndings = handledFileExtensions;

        if (HandledFileEndings.Count < 1)
            throw new ArgumentException("No file extensions provided");

        if (HandledFileEndings.Count(string.IsNullOrEmpty) > 1)
        {
            throw new AggregateException("Handling all file types (empty extension) should be specified only once");
        }
    }

    /// <summary>
    ///   Contains a list of file endings (extensions) that this check handles
    /// </summary>
    private IReadOnlyCollection<string> HandledFileEndings { get; }

    public bool HandlesFile(string file)
    {
        return HandledFileEndings.Any(file.EndsWith);
    }

    public abstract IAsyncEnumerable<ExtractedTranslation> Handle(string path, CancellationToken cancellationToken);
}
