using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Abstractions.Localization;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Media.PlanMediaSet;

/// <summary>
/// Handles <see cref="PlanMediaSetCommand"/>.
/// </summary>
/// <param name="ContentService"></param>
/// <param name="ChangePlanService"></param>
/// <param name="RulesCatalog"></param>
/// <param name="Localizer"></param>
[Log]
public record PlanMediaSetCommandHandler(IStoreContentService ContentService,
                                         IChangePlanService ChangePlanService,
                                         IRulesCatalog RulesCatalog,
                                         IMilvaLocalizer Localizer) : IInterceptable, ICommandHandler<PlanMediaSetCommand, ChangePlanDto>
{
    private readonly IStoreContentService _contentService = ContentService;
    private readonly IChangePlanService _changePlanService = ChangePlanService;
    private readonly IRulesCatalog _rulesCatalog = RulesCatalog;
    private readonly IMilvaLocalizer _localizer = Localizer;

    /// <inheritdoc/>
    public async Task<Response<ChangePlanDto>> Handle(PlanMediaSetCommand request, CancellationToken cancellationToken)
    {
        var mediaClass = Storeasy.Domain.Content.MediaClasses.All.FirstOrDefault(c => c.Kind == request.Kind && c.ClassKey == request.ClassKey);

        if (mediaClass == null)
            return Response<ChangePlanDto>.Error(null, new ResponseMessage(MessageKey.MediaClassUnknown, _localizer[MessageKey.MediaClassUnknown, request.ClassKey]));

        var read = await _contentService.ReadStoreAppAsync(request.OrganizationId, request.StoreAppId, request.Kind == MediaKind.Preview ? ContentParts.Previews : ContentParts.Screenshots, cancellationToken, store: mediaClass.Store);

        if (!read.IsSuccess)
            return read.Error<ChangePlanDto>();

        if (read.App.Store == StoreKind.Apple && !read.Snapshot.VersionEditable)
            return Response<ChangePlanDto>.Error(null, MessageKey.VersionNotEditable);

        var files = await _contentService.InspectAssetsAsync(request.OrganizationId, request.AssetIds, cancellationToken);

        if (files.Count != request.AssetIds.Distinct().Count())
            return Response<ChangePlanDto>.Error(null, MessageKey.AssetNotFound);

        var draft = MediaPlanner.Build(read.App.Store, mediaClass, request.Locale, read.Snapshot, request.Order, request.Deletes, files, request.PreviewFrameTimeCode, _rulesCatalog.Current, _localizer);

        if (draft.Items.Count == 0 && draft.Files.Count > 0 && draft.Files.All(f => f.Result == MediaPlanner.Rejected))
            return Response<ChangePlanDto>.Error(new ChangePlanDto { Files = draft.Files, Items = [], Warnings = draft.Warnings }, MessageKey.MediaRejected);

        return await _changePlanService.SaveAsync(read.App, read.Snapshot, draft, request.DryRun, null, cancellationToken);
    }
}
