using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.RegistryDtos;
using Storeasy.Domain.Registries;

namespace Storeasy.Application.Features.Registries.GetPackageSetup;

/// <summary>
/// Handles <see cref="GetPackageSetupQuery"/>. A package of another organization is reported as not found.
/// </summary>
/// <param name="packageRepository"></param>
public class GetPackageSetupQueryHandler(IStoreasyRepositoryBase<TrackedPackage> packageRepository) : IInterceptable, IQueryHandler<GetPackageSetupQuery, TrustedPublishingDto>
{
    private readonly IStoreasyRepositoryBase<TrackedPackage> _packageRepository = packageRepository;

    /// <inheritdoc/>
    public async Task<Response<TrustedPublishingDto>> Handle(GetPackageSetupQuery request, CancellationToken cancellationToken)
    {
        var package = await _packageRepository.GetFirstOrDefaultAsync(p => p.Id == request.TrackedPackageId && p.OrganizationId == request.OrganizationId, cancellationToken: cancellationToken);

        if (package == null)
            return Response<TrustedPublishingDto>.Error(null, MessageKey.TrackedPackageNotFound);

        var dto = request.Provider == null
            ? TrustedPublishingDto.Build(package, package.SetupProvider, package.SetupRepository, package.SetupWorkflow, package.SetupEnvironment, package.SetupRegistryUser, saved: package.SetupProvider != null)
            : TrustedPublishingDto.Build(package, request.Provider, request.Repository?.Trim(), request.Workflow?.Trim(), request.Environment?.Trim(), request.RegistryUser?.Trim(), saved: false);

        return Response<TrustedPublishingDto>.Success(dto);
    }
}
