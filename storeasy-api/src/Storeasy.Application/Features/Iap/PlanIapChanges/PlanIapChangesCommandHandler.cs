using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Features.Iap.PlanIapChanges;

/// <summary>
/// Handles <see cref="PlanIapChangesCommand"/>.
/// </summary>
/// <param name="MonetizationService"></param>
/// <param name="ContentService"></param>
/// <param name="ProductRepository"></param>
[Log]
public record PlanIapChangesCommandHandler(IMonetizationService MonetizationService,
                                           IStoreContentService ContentService,
                                           IStoreasyRepositoryBase<StoreProduct> ProductRepository) : IInterceptable, ICommandHandler<PlanIapChangesCommand, ChangePlanDto>
{
    private readonly IMonetizationService _monetizationService = MonetizationService;
    private readonly IStoreContentService _contentService = ContentService;
    private readonly IStoreasyRepositoryBase<StoreProduct> _productRepository = ProductRepository;

    /// <inheritdoc/>
    public Task<Response<ChangePlanDto>> Handle(PlanIapChangesCommand request, CancellationToken cancellationToken)
    {
        return _monetizationService.PlanChangesAsync(_contentService, _productRepository, request.OrganizationId, request.StoreAppId, ChangePlanKind.Monetization, request.Changes, request.DryRun, request.Confirm, cancellationToken);
    }
}
