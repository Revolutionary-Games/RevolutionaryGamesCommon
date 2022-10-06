namespace DevCenterCommunication.Models;

using System;

public abstract class ClientSideTimedModel : ClientSideModelWithCreationTime, ITimestampedModel
{
    public DateTime UpdatedAt { get; set; }
}
