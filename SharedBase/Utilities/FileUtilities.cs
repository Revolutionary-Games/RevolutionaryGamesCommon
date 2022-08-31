namespace SharedBase.Utilities;

using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

public class FileUtilities
{
    public static async Task<byte[]> CalculateSha256OfFile(string file, CancellationToken cancellationToken)
    {
        await using var reader = File.Open(file, FileMode.Open, FileAccess.Read);

        return await SHA256.Create().ComputeHashAsync(reader, cancellationToken);
    }
}
