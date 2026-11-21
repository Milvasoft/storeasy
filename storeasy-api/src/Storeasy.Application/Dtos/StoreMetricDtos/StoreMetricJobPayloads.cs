namespace Storeasy.Application.Dtos.StoreMetricDtos;

/// <summary>
/// Payload of a <see cref="StoreJobKind.SyncStoreMetrics"/> job.
/// </summary>
/// <param name="StoreAppId">Store record.</param>
public sealed record SyncStoreMetricsPayload(int StoreAppId);

/// <summary>
/// Payload of a <see cref="StoreJobKind.EnableAppleAnalytics"/> job.
/// </summary>
/// <param name="StoreAppId">App Store record.</param>
/// <param name="UserName">Member who confirmed it.</param>
public sealed record EnableAppleAnalyticsPayload(int StoreAppId, string UserName);
