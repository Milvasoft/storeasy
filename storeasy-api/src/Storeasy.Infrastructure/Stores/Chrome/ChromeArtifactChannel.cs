using Storeasy.Application.Interfaces;
using Storeasy.Domain.Chrome;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Intake;

namespace Storeasy.Infrastructure.Stores.Chrome;

/// <summary>
/// Artifact intake for the Chrome Web Store: an extension ZIP that passed the manifest checks is uploaded to the app's item
/// as its new package. Nothing is submitted for review: the owner approves a Chrome plan for that (the uploaded package is
/// what a plan without a package submits).
/// </summary>
/// <param name="client"></param>
/// <param name="fileStorage"></param>
/// <param name="itemRepository"></param>
public sealed class ChromeArtifactChannel(IChromeWebStoreClient client,
                                          IFileStorage fileStorage,
                                          IStoreasyRepositoryBase<ChromeItem> itemRepository) : IArtifactChannel
{
    private readonly IChromeWebStoreClient _client = client;
    private readonly IFileStorage _fileStorage = fileStorage;
    private readonly IStoreasyRepositoryBase<ChromeItem> _itemRepository = itemRepository;

    /// <inheritdoc/>
    public StoreKind Store => StoreKind.Chrome;

    /// <inheritdoc/>
    public (bool? Matches, string Reason) Match(ArtifactPackageInfo package, StoreAppRecord record)
        => package?.Kind != PackageKind.ExtensionZip
            ? (false, "The Chrome Web Store takes browser extension ZIP packages.")
            : (null, $"an extension ZIP carries no extension id; it is delivered to the app's item {record.ExternalId}");

    /// <inheritdoc/>
    public async Task<string> DeliverAsync(ArtifactDeliveryRequest request, IStoreJobContext context, CancellationToken cancellationToken)
    {
        ChromeUploadRead upload;

        if (request.StoreUploadId == null)
        {
            var step = await context.BeginStepAsync("chrome.upload", StepDirection.Outbound, request.ExternalId, request.Size > 0 ? request.Size : null, cancellationToken);

            await using (var package = await _fileStorage.OpenReadAsync(request.ObjectKey, cancellationToken))
            {
                upload = await _client.UploadAsync(request.SecretJson, request.ExternalId, package, request.Size, cancellationToken);
            }

            await context.EndStepAsync(step, upload.State == ChromeUploadState.Failed ? StoreJobStepStatus.Failed : StoreJobStepStatus.Succeeded, upload.State.ToString(), cancellationToken);
        }
        else
        {
            // An earlier attempt uploaded it and the store was still processing: read the status only.
            var status = await _client.FetchStatusAsync(request.SecretJson, request.ExternalId, cancellationToken);

            upload = new ChromeUploadRead(status.LastUploadState == ChromeUploadState.None ? ChromeUploadState.Succeeded : status.LastUploadState, request.Package?.VersionName);
        }

        if (upload.State == ChromeUploadState.Failed)
            throw new StoreJobException("The Chrome Web Store refused the package; the developer dashboard shows why.", transient: false);

        await RecordAsync(request, upload, cancellationToken);

        if (upload.State == ChromeUploadState.InProgress)
            return null;

        var version = upload.Version ?? request.Package?.VersionName;

        return $"Package {version} uploaded to {request.ExternalId}. Submit it for review with a Chrome plan (typed confirmation).";
    }

    /// <summary>
    /// Keeps the upload on the item, so the item page shows what a plan without a package submits.
    /// </summary>
    private async Task RecordAsync(ArtifactDeliveryRequest request, ChromeUploadRead upload, CancellationToken cancellationToken)
    {
        var item = await _itemRepository.GetFirstOrDefaultAsync(i => i.StoreAppId == request.StoreAppId && i.OrganizationId == request.OrganizationId, cancellationToken: cancellationToken);
        var version = upload.Version ?? request.Package?.VersionName;

        if (version?.Length > ChromeItem.VersionMaxLength)
            version = version[..ChromeItem.VersionMaxLength];

        if (item == null)
        {
            await _itemRepository.AddAsync(new ChromeItem
            {
                OrganizationId = request.OrganizationId,
                StoreAppId = request.StoreAppId,
                ItemId = request.ExternalId,
                UploadedVersion = version,
                LastUploadState = upload.State,
                IsPending = upload.State == ChromeUploadState.InProgress,
            }, cancellationToken);

            return;
        }

        item.UploadedVersion = version;
        item.LastUploadState = upload.State;
        item.IsPending = item.SubmittedState == ChromeItemState.PendingReview || upload.State == ChromeUploadState.InProgress;

        await _itemRepository.UpdateAsync(item, cancellationToken, i => i.UploadedVersion, i => i.LastUploadState, i => i.IsPending);
    }
}
