using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Abstractions.Localization;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Listings.PlanListing;

/// <summary>
/// Handles <see cref="PlanListingCommand"/>.
/// </summary>
/// <param name="ContentService"></param>
/// <param name="ChangePlanService"></param>
/// <param name="Localizer"></param>
[Log]
public record PlanListingCommandHandler(IStoreContentService ContentService,
                                        IChangePlanService ChangePlanService,
                                        IMilvaLocalizer Localizer) : IInterceptable, ICommandHandler<PlanListingCommand, ChangePlanDto>
{
    private readonly IStoreContentService _contentService = ContentService;
    private readonly IChangePlanService _changePlanService = ChangePlanService;
    private readonly IMilvaLocalizer _localizer = Localizer;

    /// <inheritdoc/>
    public async Task<Response<ChangePlanDto>> Handle(PlanListingCommand request, CancellationToken cancellationToken)
    {
        var read = await _contentService.ReadStoreAppAsync(request.OrganizationId, request.StoreAppId, ContentParts.Texts | ContentParts.Details, cancellationToken, request.VersionId);

        if (!read.IsSuccess)
            return read.Error<ChangePlanDto>();

        var draft = FieldPlanner.Listing(read.App.Store, read.Snapshot, request.Changes.Select(c => (c.Locale, c.Field, c.Value ?? string.Empty)), _localizer, out var errorKey);

        if (draft == null)
            return Response<ChangePlanDto>.Error(null, errorKey);

        return await _changePlanService.SaveAsync(read.App, read.Snapshot, draft, request.DryRun, null, cancellationToken);
    }
}
