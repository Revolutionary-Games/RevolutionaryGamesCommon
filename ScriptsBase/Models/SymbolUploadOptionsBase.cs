namespace ScriptsBase.Models;

using CommandLine;

public class SymbolUploadOptionsBase : DevCenterAccessingOptionsBase
{
    [Option('p', "parallel", Required = false, Default = DEFAULT_PARALLEL_UPLOADS,
        MetaValue = "COUNT", HelpText = "How many parallel uploads to do")]
    public int ParallelUploads { get; set; }
}
