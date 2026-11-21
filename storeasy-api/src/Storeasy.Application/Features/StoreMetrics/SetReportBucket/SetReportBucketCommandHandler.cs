using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Dtos.StoreMetricDtos;
using Storeasy.Application.Utils.Analytics;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Stores;

namespace Storeasy.Application.Features.StoreMetrics.SetReportBucket;

/// <summary>
/// Handles <see cref="SetReportBucketCommand"/>. A change is written and audited; a request that changes nothing writes
/// nothing. A set bucket queues a metrics sync of every Google Play record of the connection.
/// </summary>
/// <param name="ConnectionRepository"></param>
/// <param name="StoreAppRepository"></param>
/// <param name="StoreJobQueue"></param>
/// <param name="AuditTrail"></param>
public record SetReportBucketCommandHandler(IStoreasyRepositoryBase<StoreConnection> ConnectionRepository,
                                            IStoreasyRepositoryBase<StoreApp> StoreAppRepository,
                                            IStoreJobQueue StoreJobQueue,
                                            IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<SetReportBucketCommand, string>
{
    private readonly IStoreasyRepositoryBase<StoreConnection> _connectionRepository = ConnectionRepository;
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = StoreAppRepository;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<string>> Handle(SetReportBucketCommand request, CancellationToken cancellationToken)
    {
        var connection = await _connectionRepository.GetFirstOrDefaultAsync(c => c.Id == request.ConnectionId && c.OrganizationId == request.OrganizationId,
                                                                            projection: c => new StoreConnection { Id = c.Id, Store = c.Store, Name = c.Name, ReportBucket = c.ReportBucket },
                                                                            cancellationToken: cancellationToken);

        if (connection == null)
            return Response<string>.Error(null, MessageKey.StoreConnectionNotFound);

        if (connection.Store != StoreKind.Google)
            return Response<string>.Error(null, MessageKey.ReportBucketGoogleOnly);

        var bucket = ReportBuckets.Normalize(request.Bucket);

        if (bucket == null)
            return Response<string>.Error(null, MessageKey.ReportBucketInvalid);

        var stored = bucket.Length == 0 ? null : bucket;

        if (stored == connection.ReportBucket)
            return Response<string>.Success(stored);

        await _connectionRepository.ExecuteUpdateAsync(connection.Id, new SetPropertyBuilder<StoreConnection>().SetPropertyValue(c => c.ReportBucket, stored), cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.ReportBucketUpdated,
                                     connection.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                     $"Google Play connection '{connection.Name}' reports bucket {(stored == null ? "cleared" : $"set to {stored}")}.",
                                     StoreKind.Google,
                                     new { Bucket = stored, PreviousBucket = connection.ReportBucket },
                                     cancellationToken);

        if (stored != null)
        {
            var records = await _storeAppRepository.GetAllAsync(s => s.StoreConnectionId == connection.Id && s.Store == StoreKind.Google && !s.App.IsDeleted,
                                                                projection: s => new StoreApp { Id = s.Id, AppId = s.AppId },
                                                                cancellationToken: cancellationToken);

            if (records.Count > 0)
                await _storeJobQueue.EnqueueAsync([.. records.Select(r => new StoreJobRequest(request.OrganizationId, StoreJobKind.SyncStoreMetrics, new SyncStoreMetricsPayload(r.Id), StoreKind.Google, r.AppId))], cancellationToken);
        }

        return Response<string>.Success(stored);
    }
}
