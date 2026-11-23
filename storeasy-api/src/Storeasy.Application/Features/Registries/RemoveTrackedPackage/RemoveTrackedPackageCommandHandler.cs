using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Registries;
using System.Globalization;

namespace Storeasy.Application.Features.Registries.RemoveTrackedPackage;

/// <summary>
/// Handles <see cref="RemoveTrackedPackageCommand"/>. A package of another organization is reported as not found.
/// </summary>
/// <param name="PackageRepository"></param>
/// <param name="AuditTrail"></param>
[Log]
public record RemoveTrackedPackageCommandHandler(IStoreasyRepositoryBase<TrackedPackage> PackageRepository, IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<RemoveTrackedPackageCommand, int>
{
    private readonly IStoreasyRepositoryBase<TrackedPackage> _packageRepository = PackageRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(RemoveTrackedPackageCommand request, CancellationToken cancellationToken)
    {
        var package = await _packageRepository.GetFirstOrDefaultAsync(p => p.Id == request.TrackedPackageId && p.OrganizationId == request.OrganizationId,
                                                                      projection: p => new TrackedPackage { Id = p.Id, Registry = p.Registry, Name = p.Name },
                                                                      cancellationToken: cancellationToken);

        if (package == null)
            return Response<int>.Error(0, MessageKey.TrackedPackageNotFound);

        await _packageRepository.ExecuteDeleteAsync(p => p.Id == package.Id && p.OrganizationId == request.OrganizationId, cancellationToken: cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.RegistryPackageRemoved, package.Id.ToString(CultureInfo.InvariantCulture), $"{package.Registry} package {package.Name} untracked.", cancellationToken: cancellationToken);

        return Response<int>.Success(package.Id);
    }
}
