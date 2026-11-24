using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.EasDtos;

namespace Storeasy.Application.Features.Eas.GetExpoConnection;

/// <summary>
/// Handles <see cref="GetExpoConnectionQuery"/>.
/// </summary>
/// <param name="EasService"></param>
public record GetExpoConnectionQueryHandler(IEasService EasService) : IInterceptable, IQueryHandler<GetExpoConnectionQuery, ExpoConnectionDto>
{
    private readonly IEasService _easService = EasService;

    /// <inheritdoc/>
    public Task<Response<ExpoConnectionDto>> Handle(GetExpoConnectionQuery request, CancellationToken cancellationToken)
        => _easService.GetConnectionAsync(request.OrganizationId, cancellationToken);
}
