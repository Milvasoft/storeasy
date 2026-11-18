using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Listings.GetListing;

/// <summary>
/// Handles <see cref="GetListingQuery"/>.
/// </summary>
/// <param name="contentService"></param>
/// <param name="rulesCatalog"></param>
public class GetListingQueryHandler(IStoreContentService contentService,
                                    IRulesCatalog rulesCatalog) : IInterceptable, IQueryHandler<GetListingQuery, ListingDto>
{
    private readonly IStoreContentService _contentService = contentService;
    private readonly IRulesCatalog _rulesCatalog = rulesCatalog;

    /// <inheritdoc/>
    public async Task<Response<ListingDto>> Handle(GetListingQuery request, CancellationToken cancellationToken)
    {
        var read = await _contentService.ReadStoreAppAsync(request.OrganizationId, request.StoreAppId, ContentParts.Texts | ContentParts.Details, cancellationToken);

        if (!read.IsSuccess)
            return read.Error<ListingDto>();

        return Response<ListingDto>.Success(ContentViews.Listing(read.App, read.Snapshot, string.IsNullOrWhiteSpace(request.Locale) ? null : request.Locale, _rulesCatalog.Current));
    }
}
