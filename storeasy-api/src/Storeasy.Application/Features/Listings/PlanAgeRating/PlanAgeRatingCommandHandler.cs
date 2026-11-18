using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Listings.PlanAgeRating;

/// <summary>
/// Handles <see cref="PlanAgeRatingCommand"/>.
/// </summary>
/// <param name="ContentService"></param>
/// <param name="ChangePlanService"></param>
[Log]
public record PlanAgeRatingCommandHandler(IStoreContentService ContentService,
                                          IChangePlanService ChangePlanService) : IInterceptable, ICommandHandler<PlanAgeRatingCommand, ChangePlanDto>
{
    private readonly IStoreContentService _contentService = ContentService;
    private readonly IChangePlanService _changePlanService = ChangePlanService;

    /// <inheritdoc/>
    public async Task<Response<ChangePlanDto>> Handle(PlanAgeRatingCommand request, CancellationToken cancellationToken)
    {
        var read = await _contentService.ReadStoreAppAsync(request.OrganizationId, request.StoreAppId, ContentParts.AgeRating, cancellationToken, store: StoreKind.Apple);

        if (!read.IsSuccess)
            return read.Error<ChangePlanDto>();

        var desired = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (field, value) in request.Answers)
        {
            var key = Storeasy.Domain.Content.ContentKey.Of(Storeasy.Domain.Content.ContentGroups.AgeRating, null, field);

            if (!read.Snapshot.Values.ContainsKey(key))
                return Response<ChangePlanDto>.Error(null, MessageKey.FieldUnknown);

            desired[key] = value.ValueKind == System.Text.Json.JsonValueKind.Null ? null : value.GetRawText();
        }

        var draft = FieldPlanner.Build(ChangePlanKind.AgeRating, $"Age rating: {desired.Count} answers", read.Snapshot, desired, g => new Storeasy.Domain.JsonModels.PlanCall { Method = "PATCH", Path = "/v1/ageRatingDeclarations", Target = $"{g.Count()} answers" });

        return await _changePlanService.SaveAsync(read.App, read.Snapshot, draft, request.DryRun, null, cancellationToken);
    }
}
