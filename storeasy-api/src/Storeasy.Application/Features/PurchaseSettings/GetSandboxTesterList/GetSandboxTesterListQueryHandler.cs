using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.MonetizationDtos;

namespace Storeasy.Application.Features.PurchaseSettings.GetSandboxTesterList;

/// <summary>
/// Handles <see cref="GetSandboxTesterListQuery"/>.
/// </summary>
/// <param name="monetizationService"></param>
/// <param name="contentService"></param>
public class GetSandboxTesterListQueryHandler(IMonetizationService monetizationService,
                                              IStoreContentService contentService) : IInterceptable, IQueryHandler<GetSandboxTesterListQuery, List<SandboxTesterDto>>
{
    private readonly IMonetizationService _monetizationService = monetizationService;
    private readonly IStoreContentService _contentService = contentService;

    /// <inheritdoc/>
    public async Task<Response<List<SandboxTesterDto>>> Handle(GetSandboxTesterListQuery request, CancellationToken cancellationToken)
    {
        var app = await _contentService.GetStoreAppAsync(request.OrganizationId, request.StoreAppId, cancellationToken);

        if (app == null)
            return Response<List<SandboxTesterDto>>.Error(null, MessageKey.StoreAppNotFound);

        return await _monetizationService.ListSandboxTestersAsync(app, cancellationToken);
    }
}
