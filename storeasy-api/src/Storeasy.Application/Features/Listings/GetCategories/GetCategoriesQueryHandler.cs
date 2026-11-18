using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Listings.GetCategories;

/// <summary>
/// Handles <see cref="GetCategoriesQuery"/>.
/// </summary>
/// <param name="contentService"></param>
public class GetCategoriesQueryHandler(IStoreContentService contentService) : IInterceptable, IQueryHandler<GetCategoriesQuery, CategoryDto>
{
    private readonly IStoreContentService _contentService = contentService;

    /// <inheritdoc/>
    public async Task<Response<CategoryDto>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var read = await _contentService.ReadStoreAppAsync(request.OrganizationId, request.StoreAppId, ContentParts.Categories, cancellationToken, store: StoreKind.Apple);

        if (!read.IsSuccess)
            return read.Error<CategoryDto>();

        var options = await _contentService.ListOptionsAsync(read.App, StoreOptionKind.Categories, null, cancellationToken);

        string Value(string group, string field) => read.Snapshot.Get(Storeasy.Domain.Content.ContentKey.Of(group, null, field));

        return Response<CategoryDto>.Success(new CategoryDto
        {
            StoreAppId = request.StoreAppId,
            Primary = Value(Storeasy.Domain.Content.ContentGroups.AppInfo, "primaryCategory"),
            Secondary = Value(Storeasy.Domain.Content.ContentGroups.AppInfo, "secondaryCategory"),
            Copyright = Value(Storeasy.Domain.Content.ContentGroups.Version, "copyright"),
            ContentRightsDeclaration = Value(Storeasy.Domain.Content.ContentGroups.App, "contentRightsDeclaration"),
            Options = [.. options.Options.Select(o => new StoreOptionDto { Key = o.Key, Title = o.Title, Extra = o.Extra })],
        });
    }
}
