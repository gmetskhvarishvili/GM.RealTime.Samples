namespace GM.RealTime.Sample.API.Contracts;

/// <summary>Body of the queue-a-message endpoint.</summary>
internal sealed record QueueRequest(string Title, string Body);
