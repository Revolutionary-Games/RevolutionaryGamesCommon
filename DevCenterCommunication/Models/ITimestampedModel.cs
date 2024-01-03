namespace DevCenterCommunication.Models;

using System;

public interface ITimestampedModel : IIdentifiable, ITimestamped;

public interface ITimestamped
{
    public DateTime CreatedAt { get; }

    public DateTime UpdatedAt { get; set; }
}

public static class TimestampedModelHelpers
{
    public static void BumpUpdatedAt(this ITimestampedModel entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
    }
}
