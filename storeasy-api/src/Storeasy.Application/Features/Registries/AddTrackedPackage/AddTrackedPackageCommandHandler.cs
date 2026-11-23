using Microsoft.EntityFrameworkCore;
using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.RegistryDtos;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Registries;
using System.Globalization;

namespace Storeasy.Application.Features.Registries.AddTrackedPackage;

/// <summary>
/// Handles <see cref="AddTrackedPackageCommand"/>.
/// </summary>
/// <param name="PackageRepository"></param>
/// <param name="AppRepository"></param>
/// <param name="RegistryReader"></param>
/// <param name="StoreJobQueue"></param>
/// <param name="AuditTrail"></param>
/// <param name="Options"></param>
[Log]
public record AddTrackedPackageCommandHandler(IStoreasyRepositoryBase<TrackedPackage> PackageRepository,
                                              IStoreasyRepositoryBase<App> AppRepository,
                                              IPackageRegistryReader RegistryReader,
                                              IStoreJobQueue StoreJobQueue,
                                              IAuditTrail AuditTrail,
                                              RegistryOptions Options) : IInterceptable, ICommandHandler<AddTrackedPackageCommand, TrackedPackageAddedDto>
{
    private readonly IStoreasyRepositoryBase<TrackedPackage> _packageRepository = PackageRepository;
    private readonly IStoreasyRepositoryBase<App> _appRepository = AppRepository;
    private readonly IPackageRegistryReader _registryReader = RegistryReader;
    private readonly IStoreJobQueue _storeJobQueue = StoreJobQueue;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly RegistryOptions _options = Options;

    /// <inheritdoc/>
    public async Task<Response<TrackedPackageAddedDto>> Handle(AddTrackedPackageCommand request, CancellationToken cancellationToken)
    {
        if (request.AppId is int appId)
        {
            var app = await _appRepository.GetFirstOrDefaultAsync(a => a.Id == appId && a.OrganizationId == request.OrganizationId && !a.IsDeleted,
                                                                  projection: a => (int?)a.Id,
                                                                  cancellationToken: cancellationToken);

            if (!app.HasValue)
                return Response<TrackedPackageAddedDto>.Error(null, MessageKey.AppNotFound);
        }

        var count = await _packageRepository.GetAllAsync(p => p.OrganizationId == request.OrganizationId, projection: p => new TrackedPackage { Id = p.Id }, cancellationToken: cancellationToken);

        if (count.Count >= _options.MaxPackagesPerOrganization)
            return Response<TrackedPackageAddedDto>.Error(null, MessageKey.TrackedPackageLimit);

        var read = await ReadAsync(request, cancellationToken);

        if (read.Error != null)
            return Response<TrackedPackageAddedDto>.Error(null, read.Error);

        var name = string.IsNullOrWhiteSpace(read.Name) ? request.Name.Trim() : read.Name.Trim();

        var existing = await _packageRepository.GetFirstOrDefaultAsync(p => p.OrganizationId == request.OrganizationId && p.Registry == request.Registry && p.Name.ToLower() == name.ToLower(),
                                                                       projection: p => (int?)p.Id,
                                                                       cancellationToken: cancellationToken);

        if (existing.HasValue)
            return Response<TrackedPackageAddedDto>.Error(null, MessageKey.TrackedPackageAlreadyAdded);

        var package = new TrackedPackage
        {
            OrganizationId = request.OrganizationId,
            Registry = request.Registry,
            Name = name,
            AppId = request.AppId,
        };

        try
        {
            await _packageRepository.AddAsync(package, cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Response<TrackedPackageAddedDto>.Error(null, MessageKey.TrackedPackageAlreadyAdded);
        }

        var jobIds = await _storeJobQueue.EnqueueAsync([new StoreJobRequest(request.OrganizationId, StoreJobKind.RegistrySync, new RegistrySyncPayload(package.Id), AppId: request.AppId)], cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.RegistryPackageAdded, package.Id.ToString(CultureInfo.InvariantCulture), $"{request.Registry} package {name} tracked.", details: new { request.Registry, Name = name, request.AppId }, cancellationToken: cancellationToken);

        return Response<TrackedPackageAddedDto>.Success(new TrackedPackageAddedDto { Id = package.Id, Name = name, JobId = jobIds[0] });
    }

    private async Task<(string Name, string Error)> ReadAsync(AddTrackedPackageCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var read = await _registryReader.ReadPackageAsync(request.Registry, request.Name.Trim(), cancellationToken);

            return read == null ? (null, MessageKey.RegistryPackageNotFound) : (read.Name, null);
        }
        catch (StoreCallException)
        {
            return (null, MessageKey.RegistryUnreachable);
        }
    }
}
