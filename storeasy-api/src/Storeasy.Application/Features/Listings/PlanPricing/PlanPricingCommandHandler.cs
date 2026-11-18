using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Listings.PlanPricing;

/// <summary>
/// Handles <see cref="PlanPricingCommand"/>.
/// </summary>
/// <param name="ContentService"></param>
/// <param name="ChangePlanService"></param>
[Log]
public record PlanPricingCommandHandler(IStoreContentService ContentService,
                                        IChangePlanService ChangePlanService) : IInterceptable, ICommandHandler<PlanPricingCommand, ChangePlanDto>
{
    private readonly IStoreContentService _contentService = ContentService;
    private readonly IChangePlanService _changePlanService = ChangePlanService;

    /// <inheritdoc/>
    public async Task<Response<ChangePlanDto>> Handle(PlanPricingCommand request, CancellationToken cancellationToken)
    {
        var read = await _contentService.ReadStoreAppAsync(request.OrganizationId, request.StoreAppId, ContentParts.Pricing, cancellationToken, store: StoreKind.Apple);

        if (!read.IsSuccess)
            return read.Error<ChangePlanDto>();

        var desired = new Dictionary<string, string>(StringComparer.Ordinal);

        if (request.PricePointId != null)
        {
            desired[Storeasy.Domain.Content.ContentKey.Of(Storeasy.Domain.Content.ContentGroups.Pricing, null, "pricePoint")] = request.PricePointId;
            desired[Storeasy.Domain.Content.ContentKey.Of(Storeasy.Domain.Content.ContentGroups.Pricing, null, "baseTerritory")] = request.BaseTerritory?.Trim().ToUpperInvariant()
                ?? read.Snapshot.Get(Storeasy.Domain.Content.ContentKey.Of(Storeasy.Domain.Content.ContentGroups.Pricing, null, "baseTerritory")) ?? "USA";
        }

        foreach (var territory in request.Territories)
        {
            var key = Storeasy.Domain.Content.ContentKey.Of(Storeasy.Domain.Content.ContentGroups.Availability, null, territory.Code?.Trim().ToUpperInvariant());

            if (!read.Snapshot.Values.ContainsKey(key))
                return Response<ChangePlanDto>.Error(null, MessageKey.FieldUnknown);

            desired[key] = territory.Available ? "true" : "false";
        }

        var draft = FieldPlanner.Build(ChangePlanKind.Pricing, "Price and availability", read.Snapshot, desired, g => new Storeasy.Domain.JsonModels.PlanCall
        {
            Method = g.Key.Group == Storeasy.Domain.Content.ContentGroups.Pricing ? "POST" : "PATCH",
            Path = g.Key.Group == Storeasy.Domain.Content.ContentGroups.Pricing ? "/v1/appPriceSchedules" : "/v1/territoryAvailabilities",
            Target = $"{g.Count()} values",
        });

        return await _changePlanService.SaveAsync(read.App, read.Snapshot, draft, request.DryRun, null, cancellationToken);
    }
}
