using Storeasy.Application.Dtos.ChromeDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Chrome;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Chrome;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using System.Security.Cryptography;
using System.Text.Json;

namespace Storeasy.Infrastructure.Stores.Chrome;

/// <summary>
/// Applies an approved Chrome plan (<see cref="ChangePlanKind.ChromeChange"/>): uploads the package (its SHA-256 checked
/// again first, then the status read until the store has processed it) and submits the item, publishes a staged revision,
/// raises the deploy percentage, cancels the submission or rolls back. The status is read back into the item afterwards;
/// the sync then follows the review.
/// </summary>
/// <param name="client"></param>
/// <param name="contentService"></param>
/// <param name="fileStorage"></param>
/// <param name="itemRepository"></param>
/// <param name="options"></param>
/// <param name="timeProvider"></param>
public sealed class ChromePlanExtension(IChromeWebStoreClient client,
                                        IStoreContentService contentService,
                                        IFileStorage fileStorage,
                                        IStoreasyRepositoryBase<ChromeItem> itemRepository,
                                        ChromeOptions options,
                                        TimeProvider timeProvider) : IChangePlanExtension
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly IChromeWebStoreClient _client = client;
    private readonly IStoreContentService _contentService = contentService;
    private readonly IFileStorage _fileStorage = fileStorage;
    private readonly IStoreasyRepositoryBase<ChromeItem> _itemRepository = itemRepository;
    private readonly ChromeOptions _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public bool Handles(ChangePlanKind kind) => kind == ChangePlanKind.ChromeChange;

    /// <inheritdoc/>
    public Task<StoreContentRead> ReadBaseAsync(StoreAppContext app, IReadOnlyCollection<string> baseKeys, CancellationToken cancellationToken)
        => _contentService.ReadAsync(app, ContentParts.All, null, cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ApplyAsync(ChangePlanApplyRequest request, IStoreJobContext context, CancellationToken cancellationToken)
    {
        var change = JsonSerializer.Deserialize<ChromePlannedChange>(request.Changes, _json) ?? throw new StoreJobException("The plan has no Chrome change.", transient: false);
        var secret = request.Target.SecretJson;
        var itemId = change.ItemId ?? request.Target.ExternalId;
        var lines = new List<string>();
        string uploadedVersion = null;

        switch (change.Kind)
        {
            case ChromeChangeKind.Publish:
            {
                if (change.SessionId != null)
                {
                    uploadedVersion = await UploadAsync(secret, itemId, change, context, cancellationToken);
                    lines.Add($"Package {uploadedVersion ?? change.PackageVersion} uploaded.");
                }

                var step = await context.BeginStepAsync("chrome.publish", StepDirection.Outbound, itemId, cancellationToken: cancellationToken);
                var published = await _client.PublishAsync(secret, itemId, new ChromePublishRequest(change.Staged, change.DeployPercentage, change.SkipReview), cancellationToken);

                await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, published.State.ToString(), cancellationToken);
                lines.Add($"Submitted: {published.State}.");
                lines.AddRange(published.Warnings.Select(w => $"Warning: {w}"));
                break;
            }
            case ChromeChangeKind.PublishStaged:
            {
                var step = await context.BeginStepAsync("chrome.publish", StepDirection.Outbound, itemId, cancellationToken: cancellationToken);
                var published = await _client.PublishAsync(secret, itemId, new ChromePublishRequest(false, null, false), cancellationToken);

                await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, published.State.ToString(), cancellationToken);
                lines.Add($"Staged revision published: {published.State}.");
                break;
            }
            case ChromeChangeKind.DeployPercentage:
            {
                var percentage = change.DeployPercentage ?? throw new StoreJobException("The plan has no deploy percentage.", transient: false);
                var step = await context.BeginStepAsync("chrome.deploy-percentage", StepDirection.Outbound, $"{percentage}%", cancellationToken: cancellationToken);

                await _client.SetDeployPercentageAsync(secret, itemId, percentage, cancellationToken);
                await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, $"{percentage}%", cancellationToken);
                lines.Add($"Deploy percentage set to {percentage}%.");
                break;
            }
            case ChromeChangeKind.CancelSubmission:
            {
                var step = await context.BeginStepAsync("chrome.cancel", StepDirection.Outbound, itemId, cancellationToken: cancellationToken);

                await _client.CancelSubmissionAsync(secret, itemId, cancellationToken);
                await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, null, cancellationToken);
                lines.Add("Submission cancelled.");
                break;
            }
            case ChromeChangeKind.Rollback:
            {
                var step = await context.BeginStepAsync("chrome.rollback", StepDirection.Outbound, change.NewVersion, cancellationToken: cancellationToken);

                await _client.RollbackAsync(secret, itemId, new ChromeRollbackRequest(change.TargetVersion, change.NewVersion, change.Reason), cancellationToken);
                await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, change.NewVersion, cancellationToken);
                lines.Add($"Rolled back to {change.TargetVersion ?? "the last published package"} as {change.NewVersion}.");
                break;
            }
            default:
                throw new StoreJobException($"Unknown Chrome change {change.Kind}.", transient: false);
        }

        await RecordAsync(request, itemId, secret, uploadedVersion, cancellationToken);

        return lines;
    }

    /// <inheritdoc/>
    public AuditAction AuditActionOf(ChangePlanKind kind, PlanItem item) => item?.Field switch
    {
        "package" => AuditAction.ChromePackageUploaded,
        "publishedDeployPercentage" => AuditAction.ChromeDeployPercentageChanged,
        "submission" => AuditAction.ChromeSubmissionCancelled,
        "rollback" => AuditAction.ChromeRolledBack,
        "state" => AuditAction.ChromePublished,
        _ => AuditAction.ChromeSubmitted,
    };

    private async Task<string> UploadAsync(string secret, string itemId, ChromePlannedChange change, IStoreJobContext context, CancellationToken cancellationToken)
    {
        var hashStep = await context.BeginStepAsync("chrome.package.check", StepDirection.Internal, change.ObjectKey, cancellationToken: cancellationToken);

        string sha256;

        try
        {
            await using var stream = await _fileStorage.OpenReadAsync(change.ObjectKey, cancellationToken);

            sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
        }
        catch (Exception ex) when (ex is FileNotFoundException or KeyNotFoundException or InvalidOperationException)
        {
            await context.EndStepAsync(hashStep, StoreJobStepStatus.Failed, "The package is no longer stored.", cancellationToken);

            throw new StoreJobException($"The package of session {change.SessionId} is no longer stored; upload it again and plan again.", transient: false);
        }

        if (change.Sha256 != null && !string.Equals(change.Sha256, sha256, StringComparison.OrdinalIgnoreCase))
        {
            await context.EndStepAsync(hashStep, StoreJobStepStatus.Failed, "SHA-256 does not match the planned package.", cancellationToken);

            throw new StoreJobException("The stored package is not the one the plan was approved for; nothing was uploaded.", transient: false);
        }

        await context.EndStepAsync(hashStep, StoreJobStepStatus.Succeeded, sha256, cancellationToken);

        var step = await context.BeginStepAsync("chrome.upload", StepDirection.Outbound, itemId, change.Size > 0 ? change.Size : null, cancellationToken);
        ChromeUploadRead upload;

        await using (var package = await _fileStorage.OpenReadAsync(change.ObjectKey, cancellationToken))
        {
            upload = await _client.UploadAsync(secret, itemId, package, change.Size, cancellationToken);
        }

        // The store may still process the package: read the status until the upload is done.
        for (var attempt = 0; upload.State == ChromeUploadState.InProgress && attempt < _options.UploadPollAttempts; attempt++)
        {
            if (_options.UploadPollIntervalMilliseconds > 0)
                await Task.Delay(TimeSpan.FromMilliseconds(_options.UploadPollIntervalMilliseconds), _timeProvider, cancellationToken);

            var status = await _client.FetchStatusAsync(secret, itemId, cancellationToken);

            upload = upload with { State = status.LastUploadState == ChromeUploadState.None ? ChromeUploadState.InProgress : status.LastUploadState };
        }

        if (upload.State != ChromeUploadState.Succeeded)
        {
            await context.EndStepAsync(step, StoreJobStepStatus.Failed, upload.State.ToString(), cancellationToken);

            throw new StoreJobException(upload.State == ChromeUploadState.InProgress
                ? "The Chrome Web Store is still processing the package; nothing was submitted. Plan the submission again without a package once the upload shows as succeeded."
                : $"The Chrome Web Store refused the package ({upload.State}); the developer dashboard shows why.", transient: false);
        }

        await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, upload.Version ?? change.PackageVersion, cancellationToken);

        return upload.Version ?? change.PackageVersion;
    }

    private async Task RecordAsync(ChangePlanApplyRequest request, string itemId, string secret, string uploadedVersion, CancellationToken cancellationToken)
    {
        ChromeItemStatusRead status;

        try
        {
            status = await _client.FetchStatusAsync(secret, itemId, cancellationToken);
        }
        catch (StoreCallException)
        {
            // The write went through; the next sync reads the status.
            return;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var item = await _itemRepository.GetFirstOrDefaultAsync(i => i.StoreAppId == request.StoreAppId && i.OrganizationId == request.OrganizationId, cancellationToken: cancellationToken);
        var isNew = item == null;

        item ??= new ChromeItem { OrganizationId = request.OrganizationId, StoreAppId = request.StoreAppId, ItemId = itemId };

        ChromeItemStates.Merge(item, status, now);

        if (uploadedVersion != null)
            item.UploadedVersion = uploadedVersion.Length > ChromeItem.VersionMaxLength ? uploadedVersion[..ChromeItem.VersionMaxLength] : uploadedVersion;

        if (isNew)
            await _itemRepository.AddAsync(item, cancellationToken);
        else
            await _itemRepository.UpdateAsync(item, cancellationToken);
    }
}
