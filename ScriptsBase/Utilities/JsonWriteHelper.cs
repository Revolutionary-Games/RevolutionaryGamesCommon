namespace ScriptsBase.Utilities;

using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Checks.FileTypes;

public class JsonWriteHelper
{
    /// <summary>
    ///   Writes an object to a JSON file and makes sure BOM and other formatting are fine
    /// </summary>
    /// <param name="file">The file to write to</param>
    /// <param name="objectToSerialize">The object to serialize to the file</param>
    /// <param name="cancellationToken">Cancellation</param>
    /// <returns>Task for when the operation is ready</returns>
    public static async Task WriteJsonWithBom(string file, object objectToSerialize,
        CancellationToken cancellationToken)
    {
        await using var writer = File.OpenWrite(file);

        // As we use the pure serialize here, we need to manually add the BOM
        await writer.WriteAsync(BomChecker.BomBytes, cancellationToken);

        await JsonSerializer.SerializeAsync(writer, objectToSerialize, new JsonSerializerOptions
        {
            WriteIndented = true,
        }, cancellationToken);

        // Add a new line at the end as JSON writing doesn't do that by default
        writer.WriteByte((byte)'\n');
    }
}
