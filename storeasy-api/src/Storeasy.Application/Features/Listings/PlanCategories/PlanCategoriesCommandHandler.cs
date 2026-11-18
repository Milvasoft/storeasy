using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Listings.PlanCategories;

/// <summary>
/// Handles <see cref="PlanCategoriesCommand"/>.
/// </summary>
/// <param name="ContentService"></param>
/// <param name="ChangePlanService"></param>
[Log]
public record PlanCategoriesCommandHandler(IStoreContentService ContentService,
                                           IChangePlanService ChangePlanService) : IInterceptable, ICommandHandler<PlanCategoriesCommand, ChangePlanDto>
{
    private readonly IStoreContentService _contentService = ContentService;
    private readonly IChangePlanService _changePlanService = ChangePlanService;

    /// <inheritdoc/>
    public async Task<Response<ChangePlanDto>> Handle(PlanCategoriesCommand request, CancellationToken cancellationToken)
    {
        var read = await _contentService.ReadStoreAppAsync(request.OrganizationId, request.StoreAppId, ContentParts.Categories, cancellationToken, store: StoreKind.Apple);

        if (!read.IsSuccess)
            return read.Error<ChangePlanDto>();

        var desired = new Dictionary<string, string>(StringComparer.Ordinal);

        void Want(string group, string field, string value)
        {
            if (value != null)
                desired[Storeasy.Domain.Content.ContentKey.Of(group, null, field)] = value.Trim();
        }

        Want(Storeasy.Domain.Content.ContentGroups.AppInfo, "primaryCategory", request.Primary);
        Want(Storeasy.Domain.Content.ContentGroups.AppInfo, "secondaryCategory", request.Secondary);
        Want(Storeasy.Domain.Content.ContentGroups.Version, "copyright", request.Copyright);
        Want(Storeasy.Domain.Content.ContentGroups.App, "contentRightsDeclaration", request.ContentRightsDeclaration);

        if (request.Copyright != null && !read.Snapshot.VersionEditable)
            return Response<ChangePlanDto>.Error(null, MessageKey.VersionNotEditable);

        var draft = FieldPlanner.Build(ChangePlanKind.Categories, "Categories and rights", read.Snapshot, desired, g => new Storeasy.Domain.JsonModels.PlanCall
        {
            Method = "PATCH",
            Path = g.Key.Group switch { "AppInfo" => "/v1/appInfos", "Version" => "/v1/appStoreVersions", _ => "/v1/apps" },
            Target = string.Join(", ", g.Select(i => i.Field)),
        });

        return await _changePlanService.SaveAsync(read.App, read.Snapshot, draft, request.DryRun, null, cancellationToken);
    }
}
