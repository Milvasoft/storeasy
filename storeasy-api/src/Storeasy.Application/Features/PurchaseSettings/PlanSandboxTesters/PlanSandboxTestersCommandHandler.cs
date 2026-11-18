using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Dtos.MonetizationDtos;
using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Features.PurchaseSettings.PlanSandboxTesters;

/// <summary>
/// Handles <see cref="PlanSandboxTestersCommand"/>.
/// </summary>
/// <param name="MonetizationService"></param>
/// <param name="ContentService"></param>
/// <param name="ProductRepository"></param>
[Log]
public record PlanSandboxTestersCommandHandler(IMonetizationService MonetizationService,
                                               IStoreContentService ContentService,
                                               IStoreasyRepositoryBase<StoreProduct> ProductRepository) : IInterceptable, ICommandHandler<PlanSandboxTestersCommand, ChangePlanDto>
{
    private readonly IMonetizationService _monetizationService = MonetizationService;
    private readonly IStoreContentService _contentService = ContentService;
    private readonly IStoreasyRepositoryBase<StoreProduct> _productRepository = ProductRepository;

    /// <inheritdoc/>
    public Task<Response<ChangePlanDto>> Handle(PlanSandboxTestersCommand request, CancellationToken cancellationToken)
    {
        return _monetizationService.PlanChangesAsync(_contentService, _productRepository, request.OrganizationId, request.StoreAppId, ChangePlanKind.SandboxTesters, request.Changes, request.DryRun, request.Confirm, cancellationToken);
    }
}
