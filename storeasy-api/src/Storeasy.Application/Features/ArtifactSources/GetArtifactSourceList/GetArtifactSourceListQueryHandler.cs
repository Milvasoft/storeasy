using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.IntakeDtos;
using Storeasy.Domain.Intake;

namespace Storeasy.Application.Features.ArtifactSources.GetArtifactSourceList;

/// <summary>
/// Handles <see cref="GetArtifactSourceListQuery"/>.
/// </summary>
/// <param name="SourceRepository"></param>
public record GetArtifactSourceListQueryHandler(IStoreasyRepositoryBase<ArtifactPullSource> SourceRepository) : IInterceptable, IQueryHandler<GetArtifactSourceListQuery, List<ArtifactSourceDto>>
{
    private readonly IStoreasyRepositoryBase<ArtifactPullSource> _sourceRepository = SourceRepository;

    /// <inheritdoc/>
    public async Task<Response<List<ArtifactSourceDto>>> Handle(GetArtifactSourceListQuery request, CancellationToken cancellationToken)
    {
        var sources = await _sourceRepository.GetAllAsync(s => s.OrganizationId == request.OrganizationId && (request.AppId == null || s.AppId == request.AppId),
                                                          projection: s => new ArtifactSourceDto
                                                          {
                                                              Id = s.Id,
                                                              Kind = s.Kind,
                                                              AppId = s.AppId,
                                                              AppName = s.App.Name,
                                                              Url = s.Kind == ArtifactPullSourceKind.Eas ? "/api/webhooks/eas/" + s.PublicId : "/api/webhooks/artifacts/" + s.PublicId,
                                                              AllowedDomains = s.AllowedDomains,
                                                              Platforms = s.Platforms,
                                                              IncludeApk = s.IncludeApk,
                                                              SecretSetDate = s.SecretSetDate,
                                                              LastCallDate = s.LastCallDate,
                                                              LastCallResult = s.LastCallResult,
                                                          },
                                                          cancellationToken: cancellationToken);

        return Response<List<ArtifactSourceDto>>.Success([.. sources.OrderBy(s => s.AppName).ThenBy(s => s.Id)]);
    }
}
