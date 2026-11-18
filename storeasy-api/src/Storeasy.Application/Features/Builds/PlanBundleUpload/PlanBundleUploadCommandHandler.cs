using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Builds.PlanBundleUpload;

/// <summary>
/// Handles <see cref="PlanBundleUploadCommand"/>.
/// </summary>
/// <param name="ContentService"></param>
/// <param name="ChangePlanService"></param>
/// <param name="AssetRepository"></param>
[Log]
public record PlanBundleUploadCommandHandler(IStoreContentService ContentService,
                                             IChangePlanService ChangePlanService,
                                             IStoreasyRepositoryBase<Storeasy.Domain.Assets.Asset> AssetRepository) : IInterceptable, ICommandHandler<PlanBundleUploadCommand, ChangePlanDto>
{
    private readonly IStoreContentService _contentService = ContentService;
    private readonly IChangePlanService _changePlanService = ChangePlanService;
    private readonly IStoreasyRepositoryBase<Storeasy.Domain.Assets.Asset> _assetRepository = AssetRepository;

    /// <inheritdoc/>
    public async Task<Response<ChangePlanDto>> Handle(PlanBundleUploadCommand request, CancellationToken cancellationToken)
    {
        var asset = await _assetRepository.GetFirstOrDefaultAsync(a => a.Id == request.AssetId && a.OrganizationId == request.OrganizationId && a.Status == AssetStatus.Uploaded,
                                                                  projection: a => new Storeasy.Domain.Assets.Asset { Id = a.Id, ObjectKey = a.ObjectKey, FileName = a.FileName, Size = a.Size },
                                                                  cancellationToken: cancellationToken);

        if (asset == null)
            return Response<ChangePlanDto>.Error(null, MessageKey.AssetNotFound);

        var read = await _contentService.ReadStoreAppAsync(request.OrganizationId, request.StoreAppId, ContentParts.Release, cancellationToken, store: StoreKind.Google);

        if (!read.IsSuccess)
            return read.Error<ChangePlanDto>();

        var bundle = new BundleChange { AssetId = asset.Id, ObjectKey = asset.ObjectKey, FileName = asset.FileName, Size = asset.Size };
        PlanDraft draft;

        if (string.IsNullOrWhiteSpace(request.Track))
        {
            draft = new PlanDraft
            {
                Kind = ChangePlanKind.BundleUpload,
                Title = $"Upload {asset.FileName}",
                Items = [new Storeasy.Domain.JsonModels.PlanItem { Group = "Bundle", Field = "bundle", After = asset.FileName, Action = ChangeAction.Create }],
                Calls = [new Storeasy.Domain.JsonModels.PlanCall { Method = "POST", Path = "edits.bundles.upload (resumable)", Target = asset.FileName }],
                Changes = new ContentChangeSet { Bundle = bundle },
            };
        }
        else
        {
            draft = ReleasePlanner.PlayRelease(read.Snapshot, new TrackReleaseChange
            {
                Track = request.Track.Trim(),
                Status = request.Status,
                UserFraction = request.UserFraction,
                ReleaseNotes = request.ReleaseNotes,
            }, bundle);

            draft.Items.Insert(0, new Storeasy.Domain.JsonModels.PlanItem { Group = "Bundle", Field = "bundle", After = asset.FileName, Action = ChangeAction.Create });
        }

        return await _changePlanService.SaveAsync(read.App, read.Snapshot, draft, request.DryRun, request.Confirm, cancellationToken);
    }
}
