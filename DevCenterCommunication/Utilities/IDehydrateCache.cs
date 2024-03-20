namespace DevCenterCommunication.Utilities;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Models;

/// <summary>
///   Cache helper for dehydrate (devbuild) operations
/// </summary>
public interface IDehydrateCache
{
    public const string CacheFileName = "dehydrated.json";

    public IEnumerable<KeyValuePair<string, IDehydrateCacheItem>> FileItems { get; }
}

public interface IDehydrateCacheItem
{
    public string? Sha3 { get; }
    public string? SpecialType { get; }

    /// <summary>
    ///   .pck files are recursive dehydrated items and will have this
    /// </summary>
    public IDehydrateCache? DataGeneric { get; }
}

public static class DehydrateCacheExtensions
{
    /// <summary>
    ///   Gets all hashes in this cache
    /// </summary>
    /// <returns>All of the hashes</returns>
    public static ISet<string> Hashes(this IDehydrateCache cache)
    {
        var result = new HashSet<string>();

        foreach (var (_, item) in cache.FileItems)
        {
            if (!string.IsNullOrEmpty(item.Sha3))
                result.Add(item.Sha3);

            if (item.DataGeneric != null)
            {
                foreach (var subHash in item.DataGeneric.Hashes())
                {
                    result.Add(subHash);
                }
            }
        }

        return result;
    }

    public static Task WriteTo(this IDehydrateCache cache, string folder, CancellationToken cancellationToken)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.General)
        {
            // Skip writing a bunch of nulls into the data to save on space a bit
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        var serialized = JsonSerializer.Serialize(cache, options);

        return File.WriteAllTextAsync(Path.Join(folder, IDehydrateCache.CacheFileName), serialized, cancellationToken);
    }

    public static DehydratedObjectIdentification GetDehydratedObjectIdentifier(this IDehydrateCacheItem item)
    {
        if (string.IsNullOrEmpty(item.Sha3))
            throw new InvalidOperationException("ItemData object is not a single item with a hash");

        return new DehydratedObjectIdentification(item.Sha3);
    }
}

/// <summary>
///   The new Dehydrate cache format
/// </summary>
public class DehydrateCacheV2 : IDehydrateCache
{
    public DehydrateCacheV2(string baseFolder)
    {
        BaseFolder = baseFolder;
        Files = new Dictionary<string, ItemData>();
    }

    [JsonConstructor]
    public DehydrateCacheV2(Dictionary<string, ItemData> files)
    {
        BaseFolder = string.Empty;
        Files = files;
    }

    [JsonIgnore]
    public string BaseFolder { get; }

    [JsonInclude]
    [JsonPropertyName("version")]
    public int CacheVersion { get; private set; } = 2;

    [JsonInclude]
    [JsonPropertyName("files")]
    public Dictionary<string, ItemData> Files { get; private set; }

    [JsonIgnore]
    public IEnumerable<KeyValuePair<string, IDehydrateCacheItem>> FileItems =>
        Files.Select(k => new KeyValuePair<string, IDehydrateCacheItem>(k.Key, k.Value));

    public void Add(string path, string sha3Hash)
    {
        Files[ProcessPath(path)] = new ItemData(sha3Hash);
    }

    public void AddPck(string path, DehydrateCacheV2 pckCache)
    {
        Files[ProcessPath(path)] = new ItemData(pckCache);
    }

    public void VisitFiles(Action<string, IDehydrateCacheItem> callback)
    {
        foreach (var entry in Files)
        {
            callback(entry.Key, entry.Value);
        }
    }

    private string ProcessPath(string path)
    {
        if (path.Contains("\\"))
            throw new ArgumentException("Path contains backslashes");

        if (path.StartsWith(BaseFolder))
            path = path.Substring(BaseFolder.Length);

        return path.TrimStart('/');
    }

    public class ItemData : IDehydrateCacheItem
    {
        public ItemData(string sha3)
        {
            Sha3 = sha3;
        }

        public ItemData(DehydrateCacheV2 subStructure)
        {
            SpecialType = "pck";
            Data = subStructure;
        }

        [JsonConstructor]
        public ItemData()
        {
        }

        [JsonPropertyName("sha3")]
        public string? Sha3 { get; set; }

        [JsonPropertyName("special")]
        public string? SpecialType { get; set; }

        /// <summary>
        ///   .pck files are recursive dehydrated items
        /// </summary>
        [JsonPropertyName("data")]
        public DehydrateCacheV2? Data { get; set; }

        [JsonIgnore]
        public IDehydrateCache? DataGeneric => Data;
    }
}

/// <summary>
///   Legacy format for any builds before 2024-03-20
/// </summary>
public class DehydrateCacheV1 : IDehydrateCache
{
    [JsonConstructor]
    public DehydrateCacheV1(Dictionary<string, ItemData> files)
    {
        Files = files;
    }

    [JsonInclude]
    [JsonPropertyName("files")]
    public Dictionary<string, ItemData> Files { get; private set; }

    [JsonIgnore]
    public IEnumerable<KeyValuePair<string, IDehydrateCacheItem>> FileItems =>
        Files.Select(k => new KeyValuePair<string, IDehydrateCacheItem>(k.Key, k.Value));

    public class ItemData : IDehydrateCacheItem
    {
        [JsonConstructor]
        public ItemData()
        {
        }

        [JsonPropertyName("sha3")]
        public string? Sha3 { get; set; }

        [JsonPropertyName("type")]
        public string? SpecialType { get; set; } = "file";

        /// <summary>
        ///   .pck files are recursive dehydrated items
        /// </summary>
        [JsonPropertyName("data")]
        public DehydrateCacheV1? Data { get; set; }

        [JsonIgnore]
        public IDehydrateCache? DataGeneric => Data;
    }
}
