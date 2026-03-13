namespace SharedBase.Utilities;

using System.Text.Json.Serialization;

/// <summary>
///   Data from a diff operation. This data format should not be changed as this is stored in the database as that
///   would break a ton of history.
/// </summary>
public class DiffData
{
    /// <summary>
    ///   Encoded version of the diff. Is not really user-readable but is in a text-like format so kind of can be read.
    ///   Decoded with <c>DiffMatchPath.DiffFromDelta</c>
    /// </summary>
    [JsonInclude]
    [JsonPropertyName("diff")]
    public string? DiffDeltaRaw;

    /// <summary>
    ///   Blank diff with no changes
    /// </summary>
    public DiffData()
    {
    }

    [JsonConstructor]
    public DiffData(string? diffDeltaRaw)
    {
        DiffDeltaRaw = diffDeltaRaw;
    }

    /// <summary>
    ///   True when the diffed data is the same, i.e. there are no differences
    /// </summary>
    [JsonIgnore]
    public bool Empty => string.IsNullOrEmpty(DiffDeltaRaw);

    public override string ToString()
    {
        var text = $"Diff with length of {DiffDeltaRaw?.Length.ToString() ?? "None"}";

        return text;
    }
}
