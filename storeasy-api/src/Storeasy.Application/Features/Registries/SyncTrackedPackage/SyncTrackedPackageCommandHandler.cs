using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.RegistryDtos;
using Storeasy.Domain.Registries;

namespace Storeasy.Application.Features.Registries.SyncTrackedPackage;

/// <summary>
/// Handles <see cref="SyncTrackedPackageCommand"/>. A package of another organization is reported as not found.
/// </summary>
/// <param name="PackageRepository"></param>
/// <param name="StoreJobQueue"></param>
[Log]
public record SyncTrackedPackageCommandHandler(IStoreasyRepositoryBase<TrackedPackage> PackageRepository, IStoreJobQueue StoreJobQueue) : IInterceptable, ICommandHandler<SyncTrackedPackageCommand, long>
{
    private readonly IStoreasyRepositoryBase<TrackedPackage> _packageRepository = PackageRepository;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;

    /// <inheritdoc/>
    public async Task<Response<long>> Handle(SyncTrackedPackageCommand request, CancellationToken cancellationToken)
    {
        int? appId = null;

        if (request.TrackedPackageId is int packageId)
        {
            var package = await _packageRepository.GetFirstOrDefaultAsync(p => p.Id == packageId && p.OrganizationId == request.OrganizationId,
                                                                          projection: p => new TrackedPackage { Id = p.Id, AppId = p.AppId },
                                                                          cancellationToken: cancellationToken);

            if (package == null)
                return Response<long>.Error(0, MessageKey.TrackedPackageNotFound);

            appId = package.AppId;
        }

        var jobIds = await _storeJobQueue.EnqueueAsync([new StoreJobRequest(request.OrganizationId, StoreJobKind.RegistrySync, new RegistrySyncPayload(request.TrackedPackageId), AppId: appId)], cancellationToken);

        return Response<long>.Success(jobIds[0]);
    }
}
