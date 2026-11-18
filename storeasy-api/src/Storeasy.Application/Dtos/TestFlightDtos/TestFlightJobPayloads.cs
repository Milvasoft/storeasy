namespace Storeasy.Application.Dtos.TestFlightDtos;

/// <summary>
/// Payload of a <see cref="StoreJobKind.SyncTestFlight"/> job.
/// </summary>
/// <param name="StoreAppId">Store record (App Store).</param>
public sealed record SyncTestFlightPayload(int StoreAppId);

/// <summary>
/// Payload of a <see cref="StoreJobKind.UpdateTestFlight"/> job.
/// </summary>
/// <param name="StoreAppId">Store record (App Store).</param>
/// <param name="Change">The write.</param>
/// <param name="BuildId">storeasy build the change is about, if one.</param>
public sealed record UpdateTestFlightPayload(int StoreAppId, TestFlightChange Change, long? BuildId);
