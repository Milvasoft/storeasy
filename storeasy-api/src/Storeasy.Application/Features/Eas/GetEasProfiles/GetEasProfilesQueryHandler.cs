using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.EasDtos;

namespace Storeasy.Application.Features.Eas.GetEasProfiles;

/// <summary>
/// Handles <see cref="GetEasProfilesQuery"/>.
/// </summary>
/// <param name="EasService"></param>
public record GetEasProfilesQueryHandler(IEasService EasService) : IInterceptable, IQueryHandler<GetEasProfilesQuery, EasProfilesDto>
{
    private readonly IEasService _easService = EasService;

    /// <inheritdoc/>
    public Task<Response<EasProfilesDto>> Handle(GetEasProfilesQuery request, CancellationToken cancellationToken)
        => _easService.GetProfilesAsync(request.OrganizationId, request.AppId, request.GitRef, cancellationToken);
}
