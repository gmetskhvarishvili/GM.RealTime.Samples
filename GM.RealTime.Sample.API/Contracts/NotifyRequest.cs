namespace GM.RealTime.Sample.API.Contracts;

/// <summary>Body of the direct presence-aware push endpoint.</summary>
internal sealed record NotifyRequest(string Event, object? Payload);
