using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.RegistryDtos;
using Storeasy.Application.Utils.Registries;
using Storeasy.Domain.Registries;
using System.Globalization;

namespace Storeasy.Application.Features.Registries.SavePackageSetup;

/// <summary>
/// Handles <see cref="SavePackageSetupCommand"/>. A package of another organization is reported as not found.
/// </summary>
/// <param name="PackageRepository"></param>
/// <param name="AuditTrail"></param>
/// <param name="TimeProvider"></param>
[Log]
public record SavePackageSetupCommandHandler(IStoreasyRepositoryBase<TrackedPackage> PackageRepository, IAuditTrail AuditTrail, TimeProvider TimeProvider) : IInterceptable, ICommandHandler<SavePackageSetupCommand, TrustedPublishingDto>
{
    private readonly IStoreasyRepositoryBase<TrackedPackage> _packageRepository = PackageRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<TrustedPublishingDto>> Handle(SavePackageSetupCommand request, CancellationToken cancellationToken)
    {
        var package = await _packageRepository.GetFirstOrDefaultAsync(p => p.Id == request.TrackedPackageId && p.OrganizationId == request.OrganizationId, cancellationToken: cancellationToken);

        if (package == null)
            return Response<TrustedPublishingDto>.Error(null, MessageKey.TrackedPackageNotFound);

        if (request.Provider is CiProvider provider && !TrustedPublishing.Providers(package.Registry).Contains(provider))
            return Response<TrustedPublishingDto>.Error(null, MessageKey.RegistryTrustedPublishingUnsupported);

        var clear = request.Provider == null;

        package.SetupProvider = request.Provider;
        package.SetupRepository = clear ? null : request.Repository.Trim();
        package.SetupWorkflow = clear ? null : request.Workflow.Trim();
        package.SetupEnvironment = clear || string.IsNullOrWhiteSpace(request.Environment) ? null : request.Environment.Trim();
        package.SetupRegistryUser = clear || string.IsNullOrWhiteSpace(request.RegistryUser) ? null : request.RegistryUser.Trim();
        package.SetupDate = clear ? null : _timeProvider.GetUtcNow().UtcDateTime;

        await _packageRepository.UpdateAsync(package, cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.RegistrySetupSaved,
                                     package.Id.ToString(CultureInfo.InvariantCulture),
                                     clear ? $"Trusted publishing setup of {package.Name} cleared." : $"Trusted publishing setup of {package.Name}: {package.SetupRepository} {package.SetupWorkflow}.",
                                     details: new { package.Registry, package.Name, package.SetupProvider, package.SetupRepository, package.SetupWorkflow, package.SetupEnvironment },
                                     cancellationToken: cancellationToken);

        return Response<TrustedPublishingDto>.Success(TrustedPublishingDto.Build(package, package.SetupProvider, package.SetupRepository, package.SetupWorkflow, package.SetupEnvironment, package.SetupRegistryUser, saved: !clear));
    }
}
