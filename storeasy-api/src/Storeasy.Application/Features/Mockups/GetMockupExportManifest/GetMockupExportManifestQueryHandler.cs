using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Domain.Stores;

namespace Storeasy.Application.Features.Mockups.GetMockupExportManifest;

/// <summary>
/// Handles <see cref="GetMockupExportManifestQuery"/>. A job of another organization or of another kind is not found; an
/// unfinished or failed export is 409.
/// </summary>
/// <param name="jobRepository"></param>
public class GetMockupExportManifestQueryHandler(IStoreasyRepositoryBase<StoreJob> jobRepository) : IInterceptable, IQueryHandler<GetMockupExportManifestQuery, MockupExportManifest>
{
    private readonly IStoreasyRepositoryBase<StoreJob> _jobRepository = jobRepository;

    /// <inheritdoc/>
    public async Task<Response<MockupExportManifest>> Handle(GetMockupExportManifestQuery request, CancellationToken cancellationToken)
    {
        var (manifest, errorKey) = await _jobRepository.GetMockupExportManifestAsync(request.OrganizationId, request.JobId, cancellationToken);

        if (errorKey != null)
            return Response<MockupExportManifest>.Error(null, errorKey);

        return Response<MockupExportManifest>.Success(manifest);
    }
}
