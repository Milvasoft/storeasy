using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.EasDtos;

namespace Storeasy.Application.Features.Eas.GetEasApp;

/// <summary>
/// Handles <see cref="GetEasAppQuery"/>.
/// </summary>
/// <param name="EasService"></param>
public record GetEasAppQueryHandler(IEasService EasService) : IInterceptable, IQueryHandler<GetEasAppQuery, EasAppDto>
{
    private readonly IEasService _easService = EasService;

    /// <inheritdoc/>
    public Task<Response<EasAppDto>> Handle(GetEasAppQuery request, CancellationToken cancellationToken)
        => _easService.GetAppAsync(request.OrganizationId, request.AppId, cancellationToken);
}
