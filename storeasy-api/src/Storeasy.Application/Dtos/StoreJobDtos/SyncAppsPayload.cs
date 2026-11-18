namespace Storeasy.Application.Dtos.StoreJobDtos;

/// <summary>
/// Payload of a <see cref="StoreJobKind.SyncApps"/> job: the connection whose apps and builds are read.
/// </summary>
/// <param name="ConnectionId"></param>
public sealed record SyncAppsPayload(int ConnectionId);
