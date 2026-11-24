using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.EasDtos;

namespace Storeasy.Application.Features.Eas.GetEasBuild;

/// <summary>
/// Handles <see cref="GetEasBuildQuery"/>.
/// </summary>
/// <param name="EasService"></param>
public record GetEasBuildQueryHandler(IEasService EasService) : IInterceptable, IQueryHandler<GetEasBuildQuery, EasBuildDto>
{
    private readonly IEasService _easService = EasService;

    /// <inheritdoc/>
    public Task<Response<EasBuildDto>> Handle(GetEasBuildQuery request, CancellationToken cancellationToken)
        => _easService.GetBuildAsync(request.OrganizationId, request.Id, cancellationToken);
}
