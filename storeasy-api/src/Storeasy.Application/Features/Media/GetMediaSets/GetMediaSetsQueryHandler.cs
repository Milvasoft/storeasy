using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Media.GetMediaSets;

/// <summary>
/// Handles <see cref="GetMediaSetsQuery"/>.
/// </summary>
/// <param name="contentService"></param>
/// <param name="rulesCatalog"></param>
/// <param name="timeProvider"></param>
public class GetMediaSetsQueryHandler(IStoreContentService contentService,
                                      IRulesCatalog rulesCatalog,
                                      TimeProvider timeProvider) : IInterceptable, IQueryHandler<GetMediaSetsQuery, MediaSetListDto>
{
    private readonly IStoreContentService _contentService = contentService;
    private readonly IRulesCatalog _rulesCatalog = rulesCatalog;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public async Task<Response<MediaSetListDto>> Handle(GetMediaSetsQuery request, CancellationToken cancellationToken)
    {
        var read = await _contentService.ReadStoreAppAsync(request.OrganizationId, request.StoreAppId, request.Kind == MediaKind.Preview ? ContentParts.Previews : ContentParts.Screenshots, cancellationToken);

        if (!read.IsSuccess)
            return read.Error<MediaSetListDto>();

        var locale = string.IsNullOrWhiteSpace(request.Locale) ? read.Snapshot.PrimaryLocale ?? read.Snapshot.Locales.FirstOrDefault() : request.Locale;

        return Response<MediaSetListDto>.Success(ContentViews.MediaSets(read.App,
                                                                        read.Snapshot,
                                                                        request.Kind,
                                                                        locale,
                                                                        _contentService.Subject(read.App, read.Snapshot),
                                                                        _rulesCatalog.Current,
                                                                        DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime),
                                                                        System.Globalization.CultureInfo.CurrentUICulture.Name));
    }
}
