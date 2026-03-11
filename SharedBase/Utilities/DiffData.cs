namespace SharedBase.Utilities;

using System.Text.Json.Serialization;

/// <summary>
///   Data from a diff operation. This data format should not be changed as this is stored in the database as that
///   would break a ton of history.
/// </summary>
public class DiffData
{
    [JsonInclude]
    [JsonPropertyName("diff")]
    public string? UnifiedDiffText;

    [JsonInclude]
    [JsonPropertyName("winStyle")]
    public bool PreferWindowsLineEndings;

    /// <summary>
    ///   Blank diff with no changes
    /// </summary>
    public DiffData()
    {
    }

    [JsonConstructor]
    public DiffData(string? unifiedDiffText)
    {
        UnifiedDiffText = unifiedDiffText;
    }

    /// <summary>
    ///   True when the diffed data is the same, i.e. there are no differences
    /// </summary>
    [JsonIgnore]
    public bool Empty => string.IsNullOrEmpty(UnifiedDiffText);

    public override string ToString()
    {
        var text = $"Diff with length of {UnifiedDiffText?.Length.ToString() ?? "None"}";

        if (PreferWindowsLineEndings)
            text += " (windows line endings)";

        return text;
    }
}
