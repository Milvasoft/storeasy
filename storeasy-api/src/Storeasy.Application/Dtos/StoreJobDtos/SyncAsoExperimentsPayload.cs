namespace Storeasy.Application.Dtos.StoreJobDtos;

/// <summary>
/// Payload of a <see cref="StoreJobKind.SyncAsoExperiments"/> job.
/// </summary>
/// <param name="StoreAppId">App Store record whose experiments and custom product pages are read.</param>
public sealed record SyncAsoExperimentsPayload(int StoreAppId);
