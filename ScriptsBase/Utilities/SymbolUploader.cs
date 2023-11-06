namespace ScriptsBase.Utilities;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DevCenterCommunication;
using DevCenterCommunication.Models;
using Models;
using SharedBase.Utilities;
using ThriveDevCenter.Shared.Forms;

/// <summary>
///   Handles uploading extracted symbol files to the ThriveDevCenter
/// </summary>
public class SymbolUploader
{
    private readonly SymbolUploadOptionsBase options;
    private readonly string rootPathToSymbols;

    private readonly Uri url;
    private readonly string token;

    private readonly List<ThingToUpload> foundSymbolFiles = new();
    private readonly List<ThingToUpload> thingsToUpload = new();

    private readonly object outputLock = new();

    public SymbolUploader(SymbolUploadOptionsBase options, string rootPathToSymbols)
    {
        this.options = options;
        this.rootPathToSymbols = rootPathToSymbols;

        if (string.IsNullOrEmpty(options.Key))
            throw new Exception("Key to access ThriveDevCenter is required");

        token = options.Key;
        url = new Uri(options.Url);
    }

    public async Task<bool> Run(CancellationToken cancellationToken)
    {
        FindSymbolFiles();

        if (foundSymbolFiles.Count < 1)
        {
            ColourConsole.WriteWarningLine("No symbol files found. Has Godot build been run?");
            return true;
        }

        if (cancellationToken.IsCancellationRequested)
            return false;

        ColourConsole.WriteInfoLine(
            $"Checking {"symbol".PrintCount(foundSymbolFiles.Count)} if the server has them already");

        if (!await CheckSymbolsToUpload(cancellationToken))
            return false;

        if (thingsToUpload.Count < 1)
        {
            ColourConsole.WriteInfoLine("The server didn't want any of our symbols");
            return true;
        }

        var totalSize = Math.Round(thingsToUpload.Sum(t => t.Size) / (float)GlobalConstants.MEBIBYTE, 2);

        ColourConsole.WriteInfoLine(
            $"About to start upload of {"symbol".PrintCount(thingsToUpload.Count)} with total size of {totalSize} MiB");

        if (!await ConsoleHelpers.WaitForInputToContinue(cancellationToken))
        {
            ColourConsole.WriteNormalLine("Canceling");
            return false;
        }

        var uploadTasks = new List<Task<bool>>();

        foreach (var toUpload in thingsToUpload.Chunk(
                     (int)Math.Ceiling(thingsToUpload.Count / (float)options.ParallelUploads)))
        {
            uploadTasks.Add(Upload(toUpload, cancellationToken));
        }

        bool success = true;

        foreach (var task in uploadTasks)
        {
            if (!await task)
            {
                ColourConsole.WriteErrorLine("An upload task failed");
                success = false;
            }
        }

        if (success)
        {
            ColourConsole.WriteSuccessLine("Symbols have been uploaded");
        }
        else
        {
            ColourConsole.WriteErrorLine("Some uploads failed");
        }

        return success;
    }

    private void FindSymbolFiles()
    {
        var symbolsFolderPath = rootPathToSymbols;

        foreach (var symbolFile in Directory.EnumerateFiles(symbolsFolderPath, "*.sym", SearchOption.AllDirectories))
        {
            var name = symbolFile;

            if (name.StartsWith(symbolsFolderPath))
            {
                name = name.Substring(symbolsFolderPath.Length);
            }

            name = name.TrimStart('/');

            foundSymbolFiles.Add(new ThingToUpload(symbolFile, name, new FileInfo(symbolFile).Length));

            ColourConsole.WriteNormalLine($"Found symbol: {symbolFile} ({name})");
        }
    }

    private async Task<bool> CheckSymbolsToUpload(CancellationToken cancellationToken)
    {
        using var client = CreateHttpClient();

        foreach (var chunk in foundSymbolFiles.Chunk(CommunicationConstants.MAX_DEBUG_SYMBOL_OFFER_BATCH))
        {
            if (cancellationToken.IsCancellationRequested)
                return false;

            var response = await client.PostAsJsonAsync("/api/v1/DebugSymbol/offerSymbols", new DebugSymbolOfferRequest
            {
                SymbolPaths = chunk.Select(t => t.Name).ToList(),
            }, cancellationToken);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                ColourConsole.WriteErrorLine($"Failed request to offer symbols: {responseContent}");
                return false;
            }

            var responseData = JsonSerializer.Deserialize<DebugSymbolOfferResponse>(responseContent,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new NullDecodedJsonException();

            foreach (var requestedUpload in responseData.Upload)
            {
                var thing = chunk.First(t => t.Name == requestedUpload);

                var size = Math.Round(thing.Size / (float)GlobalConstants.MEBIBYTE, 2);
                ColourConsole.WriteNormalLine(
                    $"Server wants us to upload {thing.Name} (from {thing.File}) with size of {size} MiB");
                thingsToUpload.Add(thing);
            }
        }

        return true;
    }

    private async Task<bool> Upload(IEnumerable<ThingToUpload> toUpload, CancellationToken cancellationToken)
    {
        using var client = CreateHttpClient();

        // Separate client to not send our authentication headers there
        using var uploadClient = new HttpClient();

        foreach (var upload in toUpload)
        {
            if (cancellationToken.IsCancellationRequested)
                return false;

            lock (outputLock)
            {
                ColourConsole.WriteNormalLine($"Starting upload of {upload.Name}");
            }

            var response = await client.PostAsJsonAsync("/api/v1/DebugSymbol/startUpload", new DebugSymbolUploadRequest
            {
                SymbolPath = upload.Name,
                Size = upload.Size,
            }, cancellationToken);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            DebugSymbolUploadResult responseData;
            lock (outputLock)
            {
                if (!response.IsSuccessStatusCode)
                {
                    ColourConsole.WriteErrorLine(
                        $"Failed request upload start of symbol ({upload.Name}): {responseContent}");
                    return false;
                }

                responseData = JsonSerializer.Deserialize<DebugSymbolUploadResult>(responseContent,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new NullDecodedJsonException();

                if (string.IsNullOrEmpty(responseData.UploadUrl))
                {
                    ColourConsole.WriteErrorLine($"Didn't receive upload URL for symbol {upload.Name}");
                    return false;
                }

                ColourConsole.WriteNormalLine($"Putting content of {upload.File}");
            }

            // Upload to storage
            response = await uploadClient.PutAsync(responseData.UploadUrl,
                new StreamContent(File.OpenRead(upload.File)), cancellationToken);

            responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                lock (outputLock)
                {
                    ColourConsole.WriteErrorLine(
                        $"Put of file content to URL failed: {responseData.UploadUrl}, {response.StatusCode}, " +
                        $"response: {responseContent}");
                }

                return false;
            }

            // Report successful upload
            response = await client.PostAsJsonAsync("/api/v1/DebugSymbol/finishUpload", new TokenForm
            {
                Token = responseData.VerifyToken,
            }, cancellationToken);

            responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            lock (outputLock)
            {
                if (!response.IsSuccessStatusCode)
                {
                    ColourConsole.WriteErrorLine(
                        $"Failed to report upload of {upload.Name} finished, {response.StatusCode}, " +
                        $"response: {responseContent}");

                    return false;
                }

                ColourConsole.WriteSuccessLine($"Uploaded {upload.Name}");
            }
        }

        return true;
    }

    private HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            BaseAddress = url,
            Timeout = TimeSpan.FromMinutes(1),
        };

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    private class ThingToUpload
    {
        public ThingToUpload(string file, string name, long size)
        {
            File = file;
            Name = name;
            Size = size;
        }

        public string File { get; }
        public string Name { get; }
        public long Size { get; }
    }
}
