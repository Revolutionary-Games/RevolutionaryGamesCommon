namespace SharedBase.Models;

using System;

public class ThriveStartInfo
{
    public ThriveStartInfo(DateTime startedAt, string startId)
    {
        StartedAt = startedAt;
        StartId = startId;
    }

    public DateTime StartedAt { get; }

    /// <summary>
    ///   Usually a GUID that identifies the start
    /// </summary>
    public string StartId { get; }
}
