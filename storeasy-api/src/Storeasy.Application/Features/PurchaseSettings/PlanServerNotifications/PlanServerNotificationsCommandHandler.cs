using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Features.PurchaseSettings.PlanServerNotifications;

/// <summary>
/// Handles <see cref="PlanServerNotificationsCommand"/>.
/// </summary>
/// <param name="MonetizationService"></param>
/// <param name="ContentService"></param>
/// <param name="ProductRepository"></param>
[Log]
public record PlanServerNotificationsCommandHandler(IMonetizationService MonetizationService,
                                                    IStoreContentService ContentService,
                                                    IStoreasyRepositoryBase<StoreProduct> ProductRepository) : IInterceptable, ICommandHandler<PlanServerNotificationsCommand, ChangePlanDto>
{
    private readonly IMonetizationService _monetizationService = MonetizationService;
    private readonly IStoreContentService _contentService = ContentService;
    private readonly IStoreasyRepositoryBase<StoreProduct> _productRepository = ProductRepository;

    /// <inheritdoc/>
    public Task<Response<ChangePlanDto>> Handle(PlanServerNotificationsCommand request, CancellationToken cancellationToken)
    {
        List<MonetizationChangeDto> changes = [new MonetizationChangeDto { Kind = MonetizationChangeKind.ServerNotifications, Url = request.Url, SandboxUrl = request.SandboxUrl, NotificationVersion = request.Version }];

        return _monetizationService.PlanChangesAsync(_contentService, _productRepository, request.OrganizationId, request.StoreAppId, ChangePlanKind.ServerNotifications, changes, request.DryRun, null, cancellationToken);
    }
}
