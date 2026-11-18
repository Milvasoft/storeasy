using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Listings.GetListingDetails;

/// <summary>
/// Handles <see cref="GetListingDetailsQuery"/>.
/// </summary>
/// <param name="contentService"></param>
public class GetListingDetailsQueryHandler(IStoreContentService contentService) : IInterceptable, IQueryHandler<GetListingDetailsQuery, ListingDetailsDto>
{
    private readonly IStoreContentService _contentService = contentService;

    /// <inheritdoc/>
    public async Task<Response<ListingDetailsDto>> Handle(GetListingDetailsQuery request, CancellationToken cancellationToken)
    {
        var read = await _contentService.ReadStoreAppAsync(request.OrganizationId, request.StoreAppId, ContentParts.Details, cancellationToken, store: StoreKind.Google);

        if (!read.IsSuccess)
            return read.Error<ListingDetailsDto>();

        string Value(string field) => read.Snapshot.Get(Storeasy.Domain.Content.ContentKey.Of(Storeasy.Domain.Content.ContentGroups.Details, null, field));

        return Response<ListingDetailsDto>.Success(new ListingDetailsDto
        {
            StoreAppId = request.StoreAppId,
            ContactEmail = Value("contactEmail"),
            ContactPhone = Value("contactPhone"),
            ContactWebsite = Value("contactWebsite"),
            DefaultLanguage = Value("defaultLanguage"),
        });
    }
}
