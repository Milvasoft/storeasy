using Storeasy.Application.Dtos.FirefoxDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Firefox;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Firefox;
using Storeasy.Domain.JsonModels;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Storeasy.Infrastructure.Stores.Firefox;

/// <summary>
/// Applies an approved Firefox plan (<see cref="ChangePlanKind.FirefoxChange"/>): patches the listing, uploads a package
/// (its SHA-256 checked again first), waits for AMO's validation and creates the version with its release notes, notes to
/// the reviewer, license and source code, adds or removes previews, replaces the icon, or edits release notes. Every file is
/// checked against the SHA-256 the plan was approved with. The add-on is read back into storeasy afterwards; the sync then
/// follows the review.
/// </summary>
/// <param name="client"></param>
/// <param name="contentService"></param>
/// <param name="fileStorage"></param>
/// <param name="addonRepository"></param>
/// <param name="options"></param>
/// <param name="timeProvider"></param>
public sealed class FirefoxPlanExtension(IAmoClient client,
                                         IStoreContentService contentService,
                                         IFileStorage fileStorage,
                                         IStoreasyRepositoryBase<FirefoxAddon> addonRepository,
                                         FirefoxOptions options,
                                         TimeProvider timeProvider) : IChangePlanExtension
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly IAmoClient _client = client;
    private readonly IStoreContentService _contentService = contentService;
    private readonly IFileStorage _fileStorage = fileStorage;
    private readonly IStoreasyRepositoryBase<FirefoxAddon> _addonRepository = addonRepository;
    private readonly FirefoxOptions _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public bool Handles(ChangePlanKind kind) => kind == ChangePlanKind.FirefoxChange;

    /// <inheritdoc/>
    public Task<StoreContentRead> ReadBaseAsync(StoreAppContext app, IReadOnlyCollection<string> baseKeys, CancellationToken cancellationToken)
        => _contentService.ReadAsync(app, ContentParts.All, null, cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ApplyAsync(ChangePlanApplyRequest request, IStoreJobContext context, CancellationToken cancellationToken)
    {
        var change = JsonSerializer.Deserialize<FirefoxPlannedChange>(request.Changes, _json) ?? throw new StoreJobException("The plan has no Firefox change.", transient: false);
        var secret = request.Target.SecretJson;
        var addon = change.AddonId ?? request.Target.ExternalId;
        var lines = new List<string>();

        switch (change.Kind)
        {
            case FirefoxChangeKind.Listing:
            {
                var patch = new JsonObject();

                foreach (var group in change.Texts.GroupBy(t => t.Field))
                {
                    var apiName = FirefoxFields.Of(group.Key)?.ApiName ?? throw new StoreJobException($"Unknown listing field {group.Key}.", transient: false);
                    var values = new JsonObject();

                    foreach (var text in group)
                        values[text.Locale] = text.Value == null ? null : JsonValue.Create(text.Value);

                    patch[apiName] = values;
                }

                if (change.Categories != null)
                    patch["categories"] = new JsonArray([.. change.Categories.Select(c => (JsonNode)JsonValue.Create(c))]);

                var step = await context.BeginStepAsync("firefox.listing", StepDirection.Outbound, addon, cancellationToken: cancellationToken);

                await _client.UpdateAddonAsync(secret, addon, patch, cancellationToken);
                await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, $"{patch.Count} fields", cancellationToken);
                lines.Add($"Listing updated ({string.Join(", ", patch.Select(p => p.Key))}).");
                break;
            }
            case FirefoxChangeKind.Version:
                lines.AddRange(await SubmitVersionAsync(secret, addon, change, context, cancellationToken));
                break;
            case FirefoxChangeKind.Previews:
            {
                foreach (var id in change.DeletePreviews)
                {
                    var step = await context.BeginStepAsync("firefox.preview.delete", StepDirection.Outbound, id.ToString(CultureInfo.InvariantCulture), cancellationToken: cancellationToken);

                    try
                    {
                        await _client.DeletePreviewAsync(secret, addon, id, cancellationToken);
                    }
                    catch (StoreCallException ex) when (ex.StatusCode == 404)
                    {
                        // Already gone.
                    }

                    await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, null, cancellationToken);
                }

                foreach (var file in change.AddPreviews)
                {
                    await CheckAsync(file, context, cancellationToken);

                    var step = await context.BeginStepAsync("firefox.preview.add", StepDirection.Outbound, file.FileName, file.Size > 0 ? file.Size : null, cancellationToken);

                    await using var stream = await _fileStorage.OpenReadAsync(file.ObjectKey, cancellationToken);
                    var preview = await _client.AddPreviewAsync(secret, addon, new AmoFile(stream, file.FileName, file.ContentType, file.Size), null, cancellationToken);

                    await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, preview.Id.ToString(CultureInfo.InvariantCulture), cancellationToken);
                }

                lines.Add($"Previews: {change.AddPreviews.Count} added, {change.DeletePreviews.Count} removed.");
                break;
            }
            case FirefoxChangeKind.Icon:
            {
                var file = change.Icon ?? throw new StoreJobException("The plan has no icon.", transient: false);

                await CheckAsync(file, context, cancellationToken);

                var step = await context.BeginStepAsync("firefox.icon", StepDirection.Outbound, file.FileName, file.Size > 0 ? file.Size : null, cancellationToken);

                await using (var stream = await _fileStorage.OpenReadAsync(file.ObjectKey, cancellationToken))
                {
                    await _client.UploadIconAsync(secret, addon, new AmoFile(stream, file.FileName, file.ContentType, file.Size), cancellationToken);
                }

                await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, null, cancellationToken);
                lines.Add("Icon replaced (AMO resizes it in the background).");
                break;
            }
            case FirefoxChangeKind.ReleaseNotes:
            {
                var versionId = change.VersionId ?? throw new StoreJobException("The plan has no version.", transient: false);
                var step = await context.BeginStepAsync("firefox.release-notes", StepDirection.Outbound, versionId.ToString(CultureInfo.InvariantCulture), cancellationToken: cancellationToken);

                await _client.UpdateVersionAsync(secret, addon, versionId, NotesOf(change), null, cancellationToken);
                await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, null, cancellationToken);
                lines.Add("Release notes updated.");
                break;
            }
            default:
                throw new StoreJobException($"Unknown Firefox change {change.Kind}.", transient: false);
        }

        await RecordAsync(request, addon, secret, cancellationToken);

        return lines;
    }

    /// <inheritdoc/>
    public AuditAction AuditActionOf(ChangePlanKind kind, PlanItem item) => item?.Field switch
    {
        FirefoxFields.ReleaseNotes when item.Group == FirefoxPlanner.ReleaseNotesGroup => AuditAction.FirefoxReleaseNotesUpdated,
        "Package" or "Channel" or "License" or "ApprovalNotes" or "Source" or FirefoxFields.ReleaseNotes => AuditAction.FirefoxVersionSubmitted,
        "Preview" => AuditAction.FirefoxPreviewsChanged,
        "Icon" => AuditAction.FirefoxIconUpdated,
        _ => AuditAction.FirefoxListingUpdated,
    };

    private async Task<List<string>> SubmitVersionAsync(string secret, string addon, FirefoxPlannedChange change, IStoreJobContext context, CancellationToken cancellationToken)
    {
        var package = change.Package ?? throw new StoreJobException("The plan has no package.", transient: false);
        var lines = new List<string>();

        await CheckAsync(package, context, cancellationToken);

        if (change.Source != null)
            await CheckAsync(change.Source, context, cancellationToken);

        var uploadStep = await context.BeginStepAsync("firefox.upload", StepDirection.Outbound, package.FileName, package.Size > 0 ? package.Size : null, cancellationToken);
        AmoUploadRead upload;

        await using (var stream = await _fileStorage.OpenReadAsync(package.ObjectKey, cancellationToken))
        {
            upload = await _client.UploadAsync(secret, new AmoFile(stream, package.FileName, "application/x-xpinstall", package.Size), change.Channel, cancellationToken);
        }

        await context.EndStepAsync(uploadStep, StoreJobStepStatus.Succeeded, upload.Uuid, cancellationToken);

        // AMO validates the upload in the background: read it until the validation finished.
        var validationStep = await context.BeginStepAsync("firefox.validation", StepDirection.Inbound, upload.Uuid, cancellationToken: cancellationToken);

        for (var attempt = 0; !upload.Processed && attempt < _options.UploadPollAttempts; attempt++)
        {
            if (_options.UploadPollIntervalMilliseconds > 0)
                await Task.Delay(TimeSpan.FromMilliseconds(_options.UploadPollIntervalMilliseconds), _timeProvider, cancellationToken);

            upload = await _client.GetUploadAsync(secret, upload.Uuid, cancellationToken);
        }

        if (!upload.Processed)
        {
            await context.EndStepAsync(validationStep, StoreJobStepStatus.Failed, "still validating", cancellationToken);

            // Nothing was submitted; the retry uploads again (an unused upload is discarded by AMO).
            throw new StoreJobException("addons.mozilla.org is still validating the package; the job tries again.", transient: true);
        }

        if (!upload.Valid)
        {
            await context.EndStepAsync(validationStep, StoreJobStepStatus.Failed, $"{upload.Errors} errors, {upload.Warnings} warnings", cancellationToken);

            throw new StoreJobException($"AMO validation failed ({upload.Errors} errors): {string.Join(" ", upload.Messages.Take(3))}", transient: false);
        }

        await context.EndStepAsync(validationStep, StoreJobStepStatus.Succeeded, $"valid, {upload.Warnings} warnings", cancellationToken);

        if (upload.Version != null && change.PackageVersion != null && !string.Equals(upload.Version, change.PackageVersion, StringComparison.Ordinal))
            throw new StoreJobException($"AMO read version {upload.Version} from the package, the plan was approved for {change.PackageVersion}; nothing was submitted.", transient: false);

        var versionStep = await context.BeginStepAsync("firefox.version.create", StepDirection.Outbound, change.PackageVersion, cancellationToken: cancellationToken);
        AmoVersionRead version;
        var create = new AmoVersionCreate(upload.Uuid, NotesOf(change), change.ApprovalNotes, change.License, change.Compatibility ?? []);

        if (change.Source != null)
        {
            await using var source = await _fileStorage.OpenReadAsync(change.Source.ObjectKey, cancellationToken);

            version = await _client.CreateVersionAsync(secret, addon, create, new AmoFile(source, change.Source.FileName, change.Source.ContentType, change.Source.Size), cancellationToken);
        }
        else
        {
            version = await _client.CreateVersionAsync(secret, addon, create, null, cancellationToken);
        }

        await context.EndStepAsync(versionStep, StoreJobStepStatus.Succeeded, $"{version.Version} {version.FileStatus}", cancellationToken);

        lines.Add($"Version {version.Version ?? change.PackageVersion} submitted on the {AmoClient.ChannelName(change.Channel)} channel: {version.FileStatus}.");

        if (upload.Warnings > 0)
            lines.Add($"AMO validation warnings: {upload.Warnings}.");

        if (change.Source != null)
            lines.Add($"Source code {change.Source.FileName} attached.");

        return lines;
    }

    private static Dictionary<string, string> NotesOf(FirefoxPlannedChange change)
        => change.Texts.Where(t => t.Field == FirefoxFields.ReleaseNotes).ToDictionary(t => t.Locale, t => t.Value, StringComparer.Ordinal);

    private async Task CheckAsync(FirefoxPlannedFile file, IStoreJobContext context, CancellationToken cancellationToken)
    {
        var step = await context.BeginStepAsync("firefox.file.check", StepDirection.Internal, file.FileName, cancellationToken: cancellationToken);
        string sha256;

        try
        {
            await using var stream = await _fileStorage.OpenReadAsync(file.ObjectKey, cancellationToken);

            sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
        }
        catch (Exception ex) when (ex is FileNotFoundException or KeyNotFoundException or InvalidOperationException)
        {
            await context.EndStepAsync(step, StoreJobStepStatus.Failed, "The file is no longer stored.", cancellationToken);

            throw new StoreJobException($"{file.FileName} is no longer stored; upload it again and plan again.", transient: false);
        }

        if (file.Sha256 != null && !string.Equals(file.Sha256, sha256, StringComparison.OrdinalIgnoreCase))
        {
            await context.EndStepAsync(step, StoreJobStepStatus.Failed, "SHA-256 does not match the planned file.", cancellationToken);

            throw new StoreJobException($"The stored {file.FileName} is not the file the plan was approved for; nothing was sent.", transient: false);
        }

        await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, sha256, cancellationToken);
    }

    private async Task RecordAsync(ChangePlanApplyRequest request, string addon, string secret, CancellationToken cancellationToken)
    {
        FirefoxAddonState state;

        try
        {
            state = await FirefoxContentGateway.ReadStateAsync(_client, secret, addon, _options.VersionPages, cancellationToken);
        }
        catch (StoreCallException)
        {
            // The write went through; the next sync reads the add-on.
            return;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var row = await _addonRepository.GetFirstOrDefaultAsync(a => a.StoreAppId == request.StoreAppId && a.OrganizationId == request.OrganizationId, cancellationToken: cancellationToken);
        var isNew = row == null;

        row ??= new FirefoxAddon { OrganizationId = request.OrganizationId, StoreAppId = request.StoreAppId };

        FirefoxAddonStates.Merge(row, state, now);

        if (isNew)
            await _addonRepository.AddAsync(row, cancellationToken);
        else
            await _addonRepository.UpdateAsync(row, cancellationToken);
    }
}
