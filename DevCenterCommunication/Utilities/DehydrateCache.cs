namespace DevCenterCommunication.Utilities;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
///   Cache helper for dehydrate (devbuild) operations
/// </summary>
public class DehydrateCache
{
    public const string CacheFileName = "dehydrated.json";

    public DehydrateCache(string baseFolder)
    {
        BaseFolder = baseFolder;
        Files = new Dictionary<string, ItemData>();
    }

    [JsonConstructor]
    public DehydrateCache(Dictionary<string, ItemData> files)
    {
        BaseFolder = string.Empty;
        Files = files;
    }

    [JsonIgnore]
    public string BaseFolder { get; }

    [JsonInclude]
    [JsonPropertyName("files")]
    public Dictionary<string, ItemData> Files { get; private set; }

    public void Add(string path, string sha3Hash)
    {
        Files[ProcessPath(path)] = new ItemData(sha3Hash);
    }

    public void AddPck(string path, DehydrateCache pckCache)
    {
        Files[ProcessPath(path)] = new ItemData(pckCache);
    }

    /// <summary>
    ///   Gets all hashes in this cache
    /// </summary>
    /// <returns>All of the hashes</returns>
    public ISet<string> Hashes()
    {
        var result = new HashSet<string>();

        foreach (var item in Files)
        {
            if (!string.IsNullOrEmpty(item.Value.Sha3))
                result.Add(item.Value.Sha3);

            if (item.Value.Data != null)
            {
                foreach (var subHash in item.Value.Data.Hashes())
                {
                    result.Add(subHash);
                }
            }
        }

        return result;
    }

    public Task WriteTo(string folder, CancellationToken cancellationToken)
    {
        var serialized = JsonSerializer.Serialize(this);

        return File.WriteAllTextAsync(Path.Join(folder, CacheFileName), serialized, cancellationToken);
    }

    private string ProcessPath(string path)
    {
        if (path.Contains("\\"))
            throw new ArgumentException("Path contains backslashes");

        if (path.StartsWith(BaseFolder))
            path = path.Substring(BaseFolder.Length);

        return path.TrimStart('/');
    }

    public class ItemData
    {
        public ItemData(string sha3)
        {
            Sha3 = sha3;
        }

        public ItemData(DehydrateCache subStructure)
        {
            Type = "pck";
            Data = subStructure;
        }

        [JsonConstructor]
        public ItemData()
        {
        }

        [JsonPropertyName("sha3")]
        public string? Sha3 { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = "file";

        /// <summary>
        ///   .pck files are recursive dehydrated items
        /// </summary>
        [JsonPropertyName("data")]
        public DehydrateCache? Data { get; set; }
    }
}
