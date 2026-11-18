namespace Storeasy.Application.Dtos.StoreJobDtos;

/// <summary>
/// Payload of a <see cref="StoreJobKind.SyncMonetization"/> job.
/// </summary>
/// <param name="StoreAppId">Store record whose products are read.</param>
public sealed record SyncMonetizationPayload(int StoreAppId);
