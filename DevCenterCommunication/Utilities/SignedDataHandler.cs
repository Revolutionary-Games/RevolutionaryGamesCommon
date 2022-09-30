namespace DevCenterCommunication.Utilities;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

public class SignedDataHandler
{
    private readonly byte[] sizeReadBuffer = new byte[sizeof(int)];

    private readonly bool reverseBytes = !BitConverter.IsLittleEndian;

    [UnsupportedOSPlatform("browser")]
    public async Task<byte[]> CreateSignature(Stream data, string keyFile, string? keyPassword)
    {
        using var certificate = new X509Certificate2(await File.ReadAllBytesAsync(keyFile), keyPassword,
            X509KeyStorageFlags.EphemeralKeySet);

        var key = certificate.GetRSAPrivateKey();

        if (key == null)
            throw new InvalidOperationException($"Could not load RSA key from {keyFile}");

        return key.SignData(data, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);
    }

    [UnsupportedOSPlatform("browser")]
    public async Task<string?> VerifySignature(byte[] data, byte[] providedSignature,
        IEnumerable<string> allowedKeyFiles)
    {
        var now = DateTime.Now;

        bool foundCertificate = false;

        foreach (var potentialKeyFile in allowedKeyFiles)
        {
            using var certificate = new X509Certificate2(await File.ReadAllBytesAsync(potentialKeyFile),
                (SecureString?)null, X509KeyStorageFlags.EphemeralKeySet);

            // Ignore certificates that are expired or not valid yet
            if (certificate.NotBefore > now || certificate.NotAfter < now)
                continue;

            foundCertificate = true;

            var key = certificate.GetRSAPublicKey();

            if (key == null)
                throw new InvalidOperationException($"No public RSA key loadable from {potentialKeyFile}");

            if (key.VerifyData(data, providedSignature, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1))
            {
                // Signature is correct with this key
                return potentialKeyFile;
            }
        }

        if (!foundCertificate)
            throw new ArgumentException("All specified certificate files are invalid");

        return null;
    }

    public async Task WriteDataWithSignature(Stream output, Stream data, byte[] signature)
    {
        if (data.Length < 1)
            throw new ArgumentException("Data to write is empty");

        if (data.Length >= int.MaxValue)
            throw new ArgumentException("Data is too long");

        WriteNextElementSize(output, (int)data.Length);

        await data.CopyToAsync(output);

        WriteNextElementSize(output, signature.Length);
        await output.WriteAsync(signature, 0, signature.Length);
    }

    [UnsupportedOSPlatform("browser")]
    public async Task<(byte[] Data, string VerifiedWith)> ReadDataWithSignature(Stream input,
        IEnumerable<string> allowedKeyFiles)
    {
        var (data, signature) = await ReadDataWithSignature(input);

        var verifiedWith = await VerifySignature(data, signature, allowedKeyFiles);

        if (verifiedWith == null)
            throw new InvalidDataException("Signature not valid for data");

        return (data, verifiedWith);
    }

    public async Task<(byte[] Data, byte[] Signature)> ReadDataWithSignature(Stream input)
    {
        var dataLength = ReadNextElementSize(input);

        var data = new byte[dataLength];

        if (await input.ReadAsync(data, 0, dataLength) != dataLength)
            throw new IOException("Could not read specified number of payload bytes");

        var signatureLength = ReadNextElementSize(input);

        var signature = new byte[signatureLength];

        if (await input.ReadAsync(signature, 0, signatureLength) != signatureLength)
            throw new IOException("Could not read specified number of signature bytes");

        return (data, signature);
    }

    public void WriteNextElementSize(Stream stream, int size)
    {
        var lengthBytes = BitConverter.GetBytes(size);

        if (reverseBytes)
        {
            foreach (var currentByte in lengthBytes.Reverse())
            {
                stream.WriteByte(currentByte);
            }
        }
        else
        {
            stream.Write(lengthBytes, 0, lengthBytes.Length);
        }
    }

    public int ReadNextElementSize(Stream stream)
    {
        var read = stream.Read(sizeReadBuffer, 0, sizeReadBuffer.Length);

        if (read != sizeReadBuffer.Length)
            throw new IOException("Failed to read enough bytes for next length field");

        if (reverseBytes)
        {
            // A bit inefficient here but this code shouldn't really end up being used on big endian platforms
            return BitConverter.ToInt32(sizeReadBuffer.Reverse().ToArray());
        }

        return BitConverter.ToInt32(sizeReadBuffer);
    }
}
