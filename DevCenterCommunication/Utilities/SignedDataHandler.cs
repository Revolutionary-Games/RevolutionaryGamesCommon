namespace DevCenterCommunication.Utilities;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
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
        using var certificate = X509CertificateLoader.LoadPkcs12(await File.ReadAllBytesAsync(keyFile), keyPassword,
            X509KeyStorageFlags.EphemeralKeySet);

        var key = certificate.GetRSAPrivateKey();

        if (key == null)
            throw new InvalidOperationException($"Could not load RSA key from {keyFile}");

        return key.SignData(data, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);
    }

    [UnsupportedOSPlatform("browser")]
    public Task<string?> VerifySignature(byte[] data, byte[] providedSignature,
        IEnumerable<string> allowedKeyFiles)
    {
        return VerifySignature(data, providedSignature,
            allowedKeyFiles.Select(k => new Tuple<Func<Task<byte[]>>, string>(() => File.ReadAllBytesAsync(k), k)));
    }

    [UnsupportedOSPlatform("browser")]
    public async Task<string?> VerifySignature(byte[] data, byte[] providedSignature,
        IEnumerable<Tuple<Func<Task<byte[]>>, string>> allowedKeyData)
    {
        var now = DateTime.Now;

        bool foundCertificate = false;

        foreach (var (potentialKeyDataRetriever, keyName) in allowedKeyData)
        {
            using var certificate = X509CertificateLoader.LoadPkcs12(await potentialKeyDataRetriever(),
                null, X509KeyStorageFlags.EphemeralKeySet);

            // Ignore certificates that are expired or not valid yet
            if (certificate.NotBefore > now || certificate.NotAfter < now)
                continue;

            foundCertificate = true;

            var key = certificate.GetRSAPublicKey();

            if (key == null)
                throw new InvalidOperationException($"No public RSA key loadable from {keyName}");

            if (key.VerifyData(data, providedSignature, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1))
            {
                // Signature is correct with this key
                return keyName;
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
        var dataLength = await ReadNextElementSize(input);

        var data = new byte[dataLength];

        if (await ReadToBuffer(data, dataLength, input) != dataLength)
            throw new IOException("Could not read specified number of payload bytes");

        var signatureLength = await ReadNextElementSize(input);

        var signature = new byte[signatureLength];

        var readBytes = await ReadToBuffer(signature, signatureLength, input);

        if (readBytes != signatureLength)
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

    public async Task<int> ReadNextElementSize(Stream stream)
    {
        var read = await ReadToBuffer(sizeReadBuffer, sizeReadBuffer.Length, stream);

        if (read != sizeReadBuffer.Length)
            throw new IOException("Failed to read enough bytes for next length field");

        if (reverseBytes)
        {
            // A bit inefficient here but this code shouldn't really end up being used on big endian platforms
            return BitConverter.ToInt32(sizeReadBuffer.Reverse().ToArray());
        }

        return BitConverter.ToInt32(sizeReadBuffer);
    }

    private async Task<int> ReadToBuffer(byte[] buffer, int wantedBytes, Stream input)
    {
        int originalBytes = wantedBytes;
        int offset = 0;

        while (wantedBytes > 0)
        {
            var read = await input.ReadAsync(buffer, offset, wantedBytes);

            // If we read 0 bytes, assume the read will permanently now fail and quit
            if (read <= 0)
                break;

            wantedBytes -= read;
            offset += read;
        }

        return originalBytes - wantedBytes;
    }
}
