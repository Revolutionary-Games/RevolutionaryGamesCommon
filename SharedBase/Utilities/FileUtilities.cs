namespace SharedBase.Utilities;

using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using SHA3.Net;

public class FileUtilities
{
    public static async Task<byte[]> CalculateSha256OfFile(string file, CancellationToken cancellationToken)
    {
        await using var reader = File.Open(file, FileMode.Open, FileAccess.Read);

        return await SHA256.Create().ComputeHashAsync(reader, cancellationToken);
    }

    public static async Task<byte[]> CalculateSha3OfFile(string file, CancellationToken cancellationToken)
    {
        await using var reader = File.OpenRead(file);

        var sha3 = Sha3.Sha3256();
        return await sha3.ComputeHashAsync(reader, cancellationToken);
    }

    public static string HashToHex(byte[] hash)
    {
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
