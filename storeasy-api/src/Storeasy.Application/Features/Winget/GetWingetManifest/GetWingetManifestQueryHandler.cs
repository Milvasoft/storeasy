using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.WingetDtos;
using Storeasy.Application.Utils.Winget;

namespace Storeasy.Application.Features.Winget.GetWingetManifest;

/// <summary>
/// Handles <see cref="GetWingetManifestQuery"/> through the shared content read, so a store error comes back as for any
/// other store.
/// </summary>
/// <param name="contentService"></param>
/// <param name="localizer"></param>
public class GetWingetManifestQueryHandler(IStoreContentService contentService, IMilvaLocalizer localizer) : IInterceptable, IQueryHandler<GetWingetManifestQuery, WingetManifestDto>
{
    private readonly IStoreContentService _contentService = contentService;
    private readonly IMilvaLocalizer _localizer = localizer;

    /// <inheritdoc/>
    public async Task<Response<WingetManifestDto>> Handle(GetWingetManifestQuery request, CancellationToken cancellationToken)
    {
        var app = await _contentService.GetStoreAppAsync(request.OrganizationId, request.StoreAppId, cancellationToken);

        if (app == null || app.Store != StoreKind.Winget)
            return Response<WingetManifestDto>.Error(null, MessageKey.StoreAppNotFound);

        var read = await _contentService.ReadAsync(app, ContentParts.All, string.IsNullOrWhiteSpace(request.Version) ? null : request.Version.Trim(), cancellationToken);

        if (!read.IsSuccess)
        {
            return read.ErrorDetail == null
                ? Response<WingetManifestDto>.Error(null, read.ErrorKey)
                : Response<WingetManifestDto>.Error(null, new ResponseMessage(read.ErrorKey, read.ErrorDetail));
        }

        return Response<WingetManifestDto>.Success(WingetContent.ToDto(app.StoreAppId, read.Snapshot, _localizer));
    }
}
