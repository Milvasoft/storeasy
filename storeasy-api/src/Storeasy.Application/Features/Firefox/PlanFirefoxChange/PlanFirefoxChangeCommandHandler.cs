using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.Enums;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Abstractions.Localization;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Dtos.FirefoxDtos;
using Storeasy.Application.Utils.Firefox;
using Storeasy.Domain.Intake;

namespace Storeasy.Application.Features.Firefox.PlanFirefoxChange;

/// <summary>
/// Handles <see cref="PlanFirefoxChangeCommand"/>.
/// </summary>
/// <param name="ContentService"></param>
/// <param name="ChangePlanService"></param>
/// <param name="SessionRepository"></param>
/// <param name="Localizer"></param>
[Log]
public record PlanFirefoxChangeCommandHandler(IStoreContentService ContentService,
                                             IChangePlanService ChangePlanService,
                                             IStoreasyRepositoryBase<ArtifactSession> SessionRepository,
                                             IMilvaLocalizer Localizer) : IInterceptable, ICommandHandler<PlanFirefoxChangeCommand, ChangePlanDto>
{
    private readonly IStoreContentService _contentService = ContentService;
    private readonly IChangePlanService _changePlanService = ChangePlanService;
    private readonly IStoreasyRepositoryBase<ArtifactSession> _sessionRepository = SessionRepository;
    private readonly IMilvaLocalizer _localizer = Localizer;

    /// <inheritdoc/>
    public async Task<Response<ChangePlanDto>> Handle(PlanFirefoxChangeCommand request, CancellationToken cancellationToken)
    {
        var app = await _contentService.GetStoreAppAsync(request.OrganizationId, request.StoreAppId, cancellationToken);

        if (app == null || app.Store != StoreKind.Firefox)
            return Response<ChangePlanDto>.Error(null, MessageKey.StoreAppNotFound);

        var read = await _contentService.ReadAsync(app, ContentParts.All, null, cancellationToken);

        if (!read.IsSuccess)
        {
            return read.ErrorDetail == null
                ? Response<ChangePlanDto>.Error(null, read.ErrorKey)
                : Response<ChangePlanDto>.Error(null, new ResponseMessage(read.ErrorKey, read.ErrorDetail));
        }

        var input = new FirefoxPlanInput
        {
            State = FirefoxContent.FromSnapshot(read.Snapshot),
            Kind = request.Kind,
            Listing = request.Listing ?? [],
            Categories = request.Categories,
            Channel = request.Channel,
            ReleaseNotes = request.ReleaseNotes ?? [],
            ApprovalNotes = request.ApprovalNotes,
            License = request.License,
            Compatibility = request.Compatibility ?? [],
            DeletePreviews = request.DeletePreviewIds ?? [],
            Version = request.Version,
        };

        if (request.Kind == FirefoxChangeKind.Version && request.SessionId is long sessionId)
        {
            var session = await _sessionRepository.GetFirstOrDefaultAsync(s => s.Id == sessionId && s.OrganizationId == request.OrganizationId && s.AppId == app.AppId,
                                                                           projection: s => new ArtifactSession
                                                                           {
                                                                               Id = s.Id,
                                                                               FileName = s.FileName,
                                                                               ObjectKey = s.ObjectKey,
                                                                               Size = s.Size,
                                                                               Sha256 = s.Sha256,
                                                                               Package = s.Package,
                                                                               PackageDeleted = s.PackageDeleted,
                                                                           },
                                                                           cancellationToken: cancellationToken);

            if (session?.Package == null || session.Package.Kind != PackageKind.Xpi)
                return Error(new FirefoxIssue(MessageKey.FirefoxPackageNotXpi, true, sessionId));

            if (session.PackageDeleted || string.IsNullOrEmpty(session.ObjectKey))
                return Error(new FirefoxIssue(MessageKey.FirefoxPackageUnavailable, true, sessionId));

            input.Package = new FirefoxPlanPackage(session.Id,
                                                   new FirefoxPlannedFile { FileName = session.FileName, ObjectKey = session.ObjectKey, Size = session.Size, Sha256 = session.Sha256, ContentType = "application/x-xpinstall" },
                                                   session.Package);
        }

        var assetIds = (request.AddPreviewAssetIds ?? []).Concat(new[] { request.IconAssetId, request.SourceAssetId }.Where(i => i.HasValue).Select(i => i.Value)).Distinct().ToList();

        if (assetIds.Count > 0)
        {
            var inspected = (await _contentService.InspectAssetsAsync(request.OrganizationId, assetIds, cancellationToken)).ToDictionary(a => a.AssetId);
            var missing = assetIds.Where(id => !inspected.ContainsKey(id)).ToList();

            if (missing.Count > 0)
                return Error([.. missing.Select(id => new FirefoxIssue(MessageKey.FirefoxAssetNotFound, true, id))]);

            if (request.Kind == FirefoxChangeKind.Previews)
                input.AddPreviews = [.. (request.AddPreviewAssetIds ?? []).Select(id => FileOf(inspected[id]))];

            if (request.Kind == FirefoxChangeKind.Icon && request.IconAssetId is long iconId)
                input.Icon = FileOf(inspected[iconId]);

            if (request.Kind == FirefoxChangeKind.Version && request.SourceAssetId is long sourceId)
                input.Source = FileOf(inspected[sourceId]);
        }

        var result = FirefoxPlanner.Build(input);

        if (result.IsBlocked)
            return Error([.. result.Issues.Where(i => i.Blocking)]);

        var draft = result.Draft;

        draft.Warnings.AddRange(FirefoxManifest.Localize(result.Issues, _localizer).Select(i => new PlanWarning { Key = i.Issue.Key, Message = i.Message }));

        return await _changePlanService.SaveAsync(app, read.Snapshot, draft, request.DryRun, request.Confirm, cancellationToken);
    }

    private static FirefoxPlanFile FileOf(InspectedAsset asset)
        => new(new FirefoxPlannedFile { AssetId = asset.AssetId, FileName = asset.FileName, ObjectKey = asset.ObjectKey, Size = asset.Size, Sha256 = asset.Sha256, ContentType = asset.ContentType },
               asset.Image?.Width,
               asset.Image?.Height,
               asset.Image?.Format);

    private Response<ChangePlanDto> Error(params FirefoxIssue[] issues)
    {
        var localized = FirefoxManifest.Localize(issues, _localizer);
        var response = Response<ChangePlanDto>.Error(null, new ResponseMessage(localized[0].Issue.Key, localized[0].Message));

        foreach (var (_, message) in localized.Skip(1))
            response.AddMessage(message, MessageType.Error);

        return response;
    }
}
