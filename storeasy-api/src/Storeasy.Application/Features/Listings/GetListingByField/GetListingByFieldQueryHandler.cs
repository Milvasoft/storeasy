using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Listings.GetListingByField;

/// <summary>
/// Handles <see cref="GetListingByFieldQuery"/>.
/// </summary>
/// <param name="contentService"></param>
/// <param name="rulesCatalog"></param>
public class GetListingByFieldQueryHandler(IStoreContentService contentService,
                                           IRulesCatalog rulesCatalog) : IInterceptable, IQueryHandler<GetListingByFieldQuery, List<ListingFieldDto>>
{
    private readonly IStoreContentService _contentService = contentService;
    private readonly IRulesCatalog _rulesCatalog = rulesCatalog;

    /// <inheritdoc/>
    public async Task<Response<List<ListingFieldDto>>> Handle(GetListingByFieldQuery request, CancellationToken cancellationToken)
    {
        var read = await _contentService.ReadStoreAppAsync(request.OrganizationId, request.StoreAppId, ContentParts.Texts, cancellationToken);

        if (!read.IsSuccess)
            return read.Error<List<ListingFieldDto>>();

        var field = Storeasy.Domain.Content.ListingFields.Find(read.App.Store, request.Field);

        if (field == null)
            return Response<List<ListingFieldDto>>.Error(null, MessageKey.FieldUnknown);

        var locales = field.Localized ? read.Snapshot.Locales : [null];

        return Response<List<ListingFieldDto>>.Success([.. locales.Select(l => ContentViews.Field(field, l, read.Snapshot, _rulesCatalog.Current))]);
    }
}
