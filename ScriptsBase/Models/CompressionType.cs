namespace ScriptsBase.Models;

public enum CompressionType
{
    /// <summary>
    ///   Compressed using 7-zip executable
    /// </summary>
    P7Zip,

    /// <summary>
    ///   Compressed .zip using dotnet zip libraries
    /// </summary>
    Zip,
}
