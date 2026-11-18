using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Dtos.MsStoreDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Extensions;
using Storeasy.Application.Utils.Models;
using Storeasy.Application.Utils.MsStore;
using Storeasy.Domain.Assets;
using Storeasy.Domain.Enums;
using Storeasy.Domain.MsStore;
using System.Text.Json.Nodes;

namespace Storeasy.Infrastructure.Stores.Microsoft;

/// <summary>
/// The <see cref="StoreJobKind.MsApplyChange"/> job: writes one planned Microsoft Store change. It reads the store and plans
/// again; when the plan is blocked or its fingerprint differs from the approved one nothing is written. Submission edits
/// go into the pending draft storeasy created (or a new draft, a copy of the last published submission), with new files
/// uploaded as one ZIP to the draft's SAS address; the draft is sent to certification only by
/// <see cref="StoreJobKind.MsSubmit"/>. A 409 marks the product, add-on or submission as Partner Center only.
/// </summary>
/// <param name="productRepository"></param>
/// <param name="submissionRepository"></param>
/// <param name="flightRepository"></param>
/// <param name="addonRepository"></param>
/// <param name="packageRepository"></param>
/// <param name="assetRepository"></param>
/// <param name="microsoftStoreClient"></param>
/// <param name="fileStorage"></param>
/// <param name="auditTrail"></param>
/// <param name="options"></param>
/// <param name="timeProvider"></param>
public sealed class MsApplyChangeJobHandler(IStoreasyRepositoryBase<MsProduct> productRepository,
                                            IStoreasyRepositoryBase<MsSubmission> submissionRepository,
                                            IStoreasyRepositoryBase<MsFlight> flightRepository,
                                            IStoreasyRepositoryBase<MsAddon> addonRepository,
                                            IStoreasyRepositoryBase<MsInstallerPackage> packageRepository,
                                            IStoreasyRepositoryBase<Asset> assetRepository,
                                            IMicrosoftStoreClient microsoftStoreClient,
                                            IFileStorage fileStorage,
                                            IAuditTrail auditTrail,
                                            MicrosoftStoreOptions options,
                                            TimeProvider timeProvider) : IStoreJobHandler
{
    private readonly IStoreasyRepositoryBase<MsProduct> _productRepository = productRepository;
    private readonly IStoreasyRepositoryBase<MsSubmission> _submissionRepository = submissionRepository;
    private readonly IStoreasyRepositoryBase<MsFlight> _flightRepository = flightRepository;
    private readonly IStoreasyRepositoryBase<MsAddon> _addonRepository = addonRepository;
    private readonly IStoreasyRepositoryBase<MsInstallerPackage> _packageRepository = packageRepository;
    private readonly IStoreasyRepositoryBase<Asset> _assetRepository = assetRepository;
    private readonly IMicrosoftStoreClient _microsoftStoreClient = microsoftStoreClient;
    private readonly IFileStorage _fileStorage = fileStorage;
    private readonly IAuditTrail _auditTrail = auditTrail;
    private readonly MicrosoftStoreOptions _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public StoreJobKind Kind => StoreJobKind.MsApplyChange;

    /// <inheritdoc/>
    public async Task<StoreJobOutcome> ExecuteAsync(IStoreJobContext context, CancellationToken cancellationToken)
    {
        var payload = context.GetPayload<MsApplyChangePayload>() ?? throw new StoreJobException("The job has no change.", transient: false);
        var product = await _productRepository.GetMsProductAsync(context.OrganizationId, payload.StoreAppId, cancellationToken) ?? throw new StoreJobException("The Microsoft Store product was removed.", transient: false);
        var connection = (product.ConnectionId.HasValue ? await context.OpenConnectionAsync(product.ConnectionId.Value, cancellationToken) : null)
                         ?? throw new StoreJobException("The store connection was removed.", transient: false);
        var change = payload.Change;

        // 1. Plan again on fresh store content.
        var planStep = await context.BeginStepAsync("plan.check", StepDirection.Outbound, $"{change.Kind} of {product.ProductId}", cancellationToken: cancellationToken);
        var evaluation = await MsChangeEvaluator.EvaluateAsync(_microsoftStoreClient,
                                                               connection.SecretJson,
                                                               context.OrganizationId,
                                                               product,
                                                               change,
                                                               new MsChangeRepositories { Submissions = _submissionRepository, Flights = _flightRepository, Addons = _addonRepository, Assets = _assetRepository },
                                                               cancellationToken);

        if (evaluation.ErrorKey != null || evaluation.Plan.Blocked)
            throw new StoreJobException($"The change cannot be applied: {evaluation.ErrorKey ?? evaluation.Plan.BlockKey}.", transient: false);

        if (evaluation.Plan.BaseHash != payload.BaseHash)
            throw new StoreJobException("The store content changed after the plan was approved; plan again.", transient: false);

        await context.EndStepAsync(planStep, StoreJobStepStatus.Succeeded, $"{evaluation.Plan.Items.Count} items.", cancellationToken);

        // 2. Write.
        var summary = change.Kind switch
        {
            MsChangeKind.Listings when product.Kind == MsProductKind.MsiExe => await PatchMetadataAsync(context, connection.SecretJson, product, [.. change.Listings.Select(MsSubmissionJson.ProductListingPatch)], cancellationToken),
            MsChangeKind.Pricing when product.Kind == MsProductKind.MsiExe => await PatchMetadataAsync(context, connection.SecretJson, product, [MsSubmissionJson.AvailabilityPatch(change.Pricing)], cancellationToken),
            MsChangeKind.InstallerPackages => await ReplaceInstallersAsync(context, connection.SecretJson, product, change.InstallerPackages, cancellationToken),
            MsChangeKind.Rollout => await ChangeRolloutAsync(context, connection.SecretJson, product, change, cancellationToken),
            MsChangeKind.CreateFlight => await CreateFlightAsync(context, connection.SecretJson, product, change.Flight, cancellationToken),
            MsChangeKind.CreateAddon => await CreateAddonAsync(context, connection.SecretJson, product, change.Addon, cancellationToken),
            MsChangeKind.DiscardSubmission => await DiscardAsync(context, connection.SecretJson, product, change, evaluation.Content, cancellationToken),
            _ => await EditSubmissionAsync(context, connection.SecretJson, product, change, evaluation, cancellationToken),
        };

        await _auditTrail.WriteAsync(context.OrganizationId,
                                     AuditAction.MsChangeApplied,
                                     product.StoreAppId.ToString(),
                                     $"Microsoft Store change {change.Kind} of {product.ProductId} applied: {summary}",
                                     StoreKind.Microsoft,
                                     new { change.Kind, evaluation.Plan.Items, context.JobId },
                                     cancellationToken);

        return new StoreJobOutcome(summary, new { change.Kind, evaluation.Plan.Target, Items = evaluation.Plan.Items.Count });
    }

    /// <summary>
    /// Edits the pending draft (or a new one): listings, images, packages, pricing (MSIX) and add-on content.
    /// </summary>
    private async Task<string> EditSubmissionAsync(IStoreJobContext context, string secret, MsProductContext product, MsChangeInputDto change, MsChangeEvaluation evaluation, CancellationToken cancellationToken)
    {
        var target = evaluation.Plan.Target;
        var owner = MsJobSupport.Ref(target, product.ProductId, change.FlightId, change.AddonStoreId);
        var content = evaluation.Content;
        JsonObject document;
        string submissionId;

        if (content.SubmissionIsPending)
        {
            document = content.Document;
            submissionId = content.SubmissionId;
        }
        else
        {
            var createStep = await context.BeginStepAsync("submission.create", StepDirection.Outbound, cancellationToken: cancellationToken);

            document = await CallAsync(() => _microsoftStoreClient.CreateSubmissionAsync(secret, owner, cancellationToken), product, change, null);
            submissionId = MsSubmissionJson.String(document, "id") ?? throw new StoreJobException("The new submission has no id.", transient: false);

            // Recorded right away: if a later step fails, the draft is still known as storeasy's own.
            await SaveDraftAsync(context.OrganizationId, product, change, target, submissionId, document, cancellationToken);
            await context.EndStepAsync(createStep, StoreJobStepStatus.Succeeded, submissionId, cancellationToken);
        }

        var files = await ApplyContentAsync(context.OrganizationId, document, target, change);

        var writeStep = await context.BeginStepAsync("submission.update", StepDirection.Outbound, submissionId, cancellationToken: cancellationToken);
        var stored = await CallAsync(() => _microsoftStoreClient.UpdateSubmissionAsync(secret, owner.WithSubmission(submissionId), document, cancellationToken), product, change, submissionId);

        await context.EndStepAsync(writeStep, StoreJobStepStatus.Succeeded, null, cancellationToken);

        if (files.Count > 0)
            await UploadFilesAsync(context, MsSubmissionJson.String(stored, "fileUploadUrl") ?? MsSubmissionJson.String(document, "fileUploadUrl"), files, cancellationToken);

        await SaveDraftAsync(context.OrganizationId, product, change, target, submissionId, stored, cancellationToken);

        return $"draft {submissionId} updated ({files.Count} files).";
    }

    /// <summary>
    /// Writes the change into the submission document and returns the assets to upload (ZIP entry name, object key).
    /// </summary>
    private async Task<List<(string EntryName, string ObjectKey)>> ApplyContentAsync(int organizationId, JsonObject document, MsSubmissionTarget target, MsChangeInputDto change)
    {
        switch (change.Kind)
        {
            case MsChangeKind.Listings:
                foreach (var listing in change.Listings)
                    MsSubmissionJson.WriteListing(document, listing);

                return [];
            case MsChangeKind.Pricing:
                MsSubmissionJson.WritePricing(document, change.Pricing);
                return [];
            case MsChangeKind.AddonContent:
                MsSubmissionJson.WriteAddonContent(document, change.AddonContent);
                return [];
            case MsChangeKind.Images:
            {
                var assets = await AssetsAsync(organizationId, [.. change.Images.Add.Select(i => i.AssetId)], MsSubmissionJson.ImagesFolder);

                MsSubmissionJson.WriteImages(document,
                                             change.Images.Locale,
                                             [.. change.Images.RemoveFileNames ?? []],
                                             change.Images.Add.Select(i => (assets[i.AssetId].EntryName, i.ImageType, i.Description)));

                return [.. assets.Values];
            }
            default:
            {
                var assets = await AssetsAsync(organizationId, change.Packages.AddAssetIds ?? [], MsSubmissionJson.PackagesFolder);

                MsSubmissionJson.WritePackages(document, target, [.. change.Packages.RemoveFileNames ?? []], (change.Packages.AddAssetIds ?? []).Select(id => assets[id].EntryName));

                if (target == MsSubmissionTarget.App && change.Packages.RolloutPercentage.HasValue)
                    MsSubmissionJson.WriteRollout(document, change.Packages.RolloutPercentage);

                return [.. assets.Values];
            }
        }
    }

    private async Task<Dictionary<long, (string EntryName, string ObjectKey)>> AssetsAsync(int organizationId, List<long> ids, string folder)
    {
        if (ids.Count == 0)
            return [];

        var assets = await _assetRepository.GetAllAsync(a => ids.Contains(a.Id) && a.OrganizationId == organizationId,
                                                        projection: a => new Asset { Id = a.Id, FileName = a.FileName, ObjectKey = a.ObjectKey });

        return assets.ToDictionary(a => a.Id, a => (MsSubmissionJson.ZipEntryName(folder, a.Id, a.FileName), a.ObjectKey));
    }

    private async Task UploadFilesAsync(IStoreJobContext context, string fileUploadUrl, List<(string EntryName, string ObjectKey)> files, CancellationToken cancellationToken)
    {
        var zipStep = await context.BeginStepAsync("files.zip", StepDirection.Internal, total: files.Count, cancellationToken: cancellationToken);
        var path = await MsJobSupport.BuildZipAsync(_microsoftStoreClient, _fileStorage, files, cancellationToken);

        try
        {
            await context.EndStepAsync(zipStep, StoreJobStepStatus.Succeeded, $"{files.Count} files.", cancellationToken);

            var uploadStep = await context.BeginStepAsync("files.upload", StepDirection.Outbound, total: new FileInfo(path).Length, cancellationToken: cancellationToken);

            await using (var zip = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true))
                await _microsoftStoreClient.UploadSubmissionFilesAsync(fileUploadUrl, zip, cancellationToken);

            await context.EndStepAsync(uploadStep, StoreJobStepStatus.Succeeded, null, cancellationToken);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Records the draft as storeasy's own, with the fingerprint of what was written, and makes it the owner's pending
    /// submission.
    /// </summary>
    private async Task SaveDraftAsync(int organizationId, MsProductContext product, MsChangeInputDto change, MsSubmissionTarget target, string submissionId, JsonObject document, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        await _submissionRepository.UpsertSubmissionAsync(organizationId, product.StoreAppId, target, submissionId, row =>
        {
            MsRowWriter.Observe(row, MsSubmissionJson.ParseStatus(MsSubmissionJson.String(document, "status")), MsSubmissionJson.ReadStatusDetails(document["statusDetails"]), now);

            row.CreatedByStoreasy = true;
            row.IsPending = true;
            row.ContentHash = MsSubmissionJson.ContentHash(document);
            row.FlightId = change.FlightId;
            row.AddonStoreId = change.AddonStoreId;
            row.FriendlyName = MsSubmissionJson.String(document, "friendlyName") ?? row.FriendlyName;
            row.SyncedDate = now;
        }, cancellationToken);

        await SetPendingAsync(product, target, change.FlightId, change.AddonStoreId, submissionId, cancellationToken);
    }

    private async Task SetPendingAsync(MsProductContext product, MsSubmissionTarget target, string flightId, string addonStoreId, string submissionId, CancellationToken cancellationToken)
    {
        switch (target)
        {
            case MsSubmissionTarget.Flight:
                await _flightRepository.ExecuteUpdateAsync(f => f.StoreAppId == product.StoreAppId && f.FlightId == flightId,
                                                           new SetPropertyBuilder<MsFlight>().SetPropertyValue(f => f.PendingSubmissionId, submissionId),
                                                           cancellationToken);
                break;
            case MsSubmissionTarget.Addon:
                await _addonRepository.ExecuteUpdateAsync(a => a.StoreAppId == product.StoreAppId && a.StoreId == addonStoreId,
                                                          new SetPropertyBuilder<MsAddon>().SetPropertyValue(a => a.PendingSubmissionId, submissionId),
                                                          cancellationToken);
                break;
            default:
                await _productRepository.ExecuteUpdateAsync(p => p.StoreAppId == product.StoreAppId,
                                                            new SetPropertyBuilder<MsProduct>().SetPropertyValue(p => p.PendingSubmissionId, submissionId),
                                                            cancellationToken);
                break;
        }
    }

    /// <summary>
    /// Runs one write; a 409 records the Partner Center rule that caused it and fails the job permanently.
    /// </summary>
    private async Task<JsonObject> CallAsync(Func<Task<JsonObject>> call, MsProductContext product, MsChangeInputDto change, string submissionId)
    {
        try
        {
            return await call();
        }
        catch (StoreCallException ex) when (ex.StatusCode == 409)
        {
            await MarkRefusedAsync(product, change, submissionId);

            throw new StoreJobException($"Microsoft refused the change (409); it belongs to Partner Center now: {ex.Message}", transient: false, ex);
        }
    }

    private async Task MarkRefusedAsync(MsProductContext product, MsChangeInputDto change, string submissionId)
    {
        if (change.Kind == MsChangeKind.AddonContent)
        {
            await _addonRepository.ExecuteUpdateAsync(a => a.StoreAppId == product.StoreAppId && a.StoreId == change.AddonStoreId,
                                                      new SetPropertyBuilder<MsAddon>().SetPropertyValue(a => a.ApiWritable, false)
                                                                                       .SetPropertyValue(a => a.AssistedReason, (MsAssistedReason?)MsAssistedReason.StoreManagedConsumable),
                                                      CancellationToken.None);
        }
        else if (submissionId != null)
        {
            await _submissionRepository.MarkModifiedOutsideApiAsync(product.StoreAppId, MsChangePlanner.TargetOf(product.Kind, change), submissionId, CancellationToken.None);
        }
        else
        {
            await _productRepository.ExecuteUpdateAsync(p => p.StoreAppId == product.StoreAppId,
                                                        new SetPropertyBuilder<MsProduct>().SetPropertyValue(p => p.HasMandatoryUpdate, true),
                                                        CancellationToken.None);
        }
    }

    private async Task<string> PatchMetadataAsync(IStoreJobContext context, string secret, MsProductContext product, List<JsonObject> patches, CancellationToken cancellationToken)
    {
        var step = await context.BeginStepAsync("metadata.patch", StepDirection.Outbound, total: patches.Count, cancellationToken: cancellationToken);

        for (var i = 0; i < patches.Count; i++)
        {
            await _microsoftStoreClient.UpdateProductMetadataAsync(secret, product.ProductId, patches[i], cancellationToken);
            await context.ReportProgressAsync(step, i + 1, cancellationToken);
        }

        await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, null, cancellationToken);

        return $"{patches.Count} metadata updates.";
    }

    private async Task<string> ReplaceInstallersAsync(IStoreJobContext context, string secret, MsProductContext product, List<MsInstallerPackageInputDto> packages, CancellationToken cancellationToken)
    {
        // 1. Authenticode: the Store rejects unsigned installers, so nothing is written unless every one is signed.
        var known = await _packageRepository.GetAllAsync(p => p.StoreAppId == product.StoreAppId, cancellationToken: cancellationToken);
        var signedUrls = known.Where(p => p.SignatureStatus == MsSignatureStatus.Signed).Select(p => p.PackageUrl).ToHashSet(StringComparer.Ordinal);
        var toCheck = packages.Select(p => p.PackageUrl).Where(u => !signedUrls.Contains(u)).Distinct().ToList();
        var checkStep = await context.BeginStepAsync("installers.check", StepDirection.Inbound, total: toCheck.Count, cancellationToken: cancellationToken);
        var results = new Dictionary<string, (MsSignatureStatus Status, string Note)>(StringComparer.Ordinal);

        for (var i = 0; i < toCheck.Count; i++)
        {
            results[toCheck[i]] = await MsJobSupport.CheckInstallerAsync(_microsoftStoreClient, toCheck[i], _options.InstallerCheckMaxBytes, cancellationToken);
            await context.ReportProgressAsync(checkStep, i + 1, cancellationToken);
        }

        var unsigned = results.Where(r => r.Value.Status != MsSignatureStatus.Signed).ToList();

        await context.EndStepAsync(checkStep,
                                   unsigned.Count == 0 ? StoreJobStepStatus.Succeeded : StoreJobStepStatus.Failed,
                                   unsigned.Count == 0 ? $"{results.Count} checked, all signed." : string.Join(" | ", unsigned.Select(u => $"{u.Key}: {u.Value.Status} {u.Value.Note}")),
                                   cancellationToken);

        if (unsigned.Count > 0)
            throw new StoreJobException($"{unsigned.Count} installer(s) are not Authenticode signed; nothing was written.", transient: false);

        // 2. Replace the package set and commit it; readiness is polled.
        var records = packages.Select(p => new MsInstallerPackageRecord
        {
            PackageUrl = p.PackageUrl,
            PackageType = p.PackageType,
            Architectures = [p.Architecture],
            Languages = p.Languages,
            IsSilentInstall = p.IsSilentInstall,
            InstallerParameters = p.InstallerParameters,
            GenericDocUrl = p.GenericDocUrl,
        }).ToList();

        var writeStep = await context.BeginStepAsync("packages.update", StepDirection.Outbound, total: records.Count, cancellationToken: cancellationToken);

        await _microsoftStoreClient.ReplaceInstallerPackagesAsync(secret, product.ProductId, records, cancellationToken);
        await _microsoftStoreClient.CommitInstallerPackagesAsync(secret, product.ProductId, cancellationToken);

        var ready = await PollReadyAsync(secret, product.ProductId, cancellationToken);

        await context.EndStepAsync(writeStep, StoreJobStepStatus.Succeeded, ready ? "Ready." : "Committed; readiness pending.", cancellationToken);

        // 3. Save the rows with the check results.
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var stored = await _microsoftStoreClient.GetInstallerPackagesAsync(secret, product.ProductId, cancellationToken);
        var rows = MsInstallerRows.Merge(context.OrganizationId, [(product.StoreAppId, stored.Count > 0 ? stored : records)], known, now);

        foreach (var row in rows.Updated.Concat(rows.Added).Where(r => results.ContainsKey(r.PackageUrl)))
        {
            row.SignatureStatus = results[row.PackageUrl].Status;
            row.SignatureNote = Trim(results[row.PackageUrl].Note, MsInstallerPackage.SignatureNoteMaxLength);
            row.CheckedDate = now;
        }

        if (rows.Updated.Count > 0)
            await _packageRepository.UpdateAsync(rows.Updated, cancellationToken, p => p.PackageId, p => p.Architectures, p => p.Languages, p => p.InstallerParameters, p => p.IsSilentInstall, p => p.GenericDocUrl, p => p.PackageType, p => p.SignatureStatus, p => p.SignatureNote, p => p.CheckedDate, p => p.SyncedDate);

        if (rows.Added.Count > 0)
            await _packageRepository.AddRangeAsync(rows.Added, cancellationToken);

        if (rows.Removed.Count > 0)
            await _packageRepository.ExecuteDeleteAsync(p => rows.Removed.Contains(p.Id), cancellationToken: cancellationToken);

        await _productRepository.ExecuteUpdateAsync(p => p.StoreAppId == product.StoreAppId, new SetPropertyBuilder<MsProduct>().SetPropertyValue(p => p.IsReady, ready), cancellationToken);

        return $"{records.Count} installer packages set{(ready ? ", product ready" : string.Empty)}.";
    }

    private async Task<bool> PollReadyAsync(string secret, string productId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < Math.Max(1, _options.StatusPollAttempts); attempt++)
        {
            var status = await _microsoftStoreClient.GetProductStatusAsync(secret, productId, cancellationToken);

            if (status.Errors.Any(e => string.Equals(e.Code, "packageuploaderror", StringComparison.OrdinalIgnoreCase)))
                throw new StoreJobException($"The package upload failed: {string.Join("; ", status.Errors.Select(e => e.Details))}", transient: true);

            if (status.IsReady)
                return true;

            await Task.Delay(TimeSpan.FromMilliseconds(_options.StatusPollIntervalMilliseconds), _timeProvider, cancellationToken);
        }

        return false;
    }

    private async Task<string> ChangeRolloutAsync(IStoreJobContext context, string secret, MsProductContext product, MsChangeInputDto change, CancellationToken cancellationToken)
    {
        var target = string.IsNullOrEmpty(change.FlightId) ? MsSubmissionTarget.App : MsSubmissionTarget.Flight;
        var step = await context.BeginStepAsync("rollout.change", StepDirection.Outbound, $"{change.Rollout.Action} {change.Rollout.Percentage}", cancellationToken: cancellationToken);
        var rollout = await _microsoftStoreClient.ChangeRolloutAsync(secret, MsJobSupport.Ref(target, product.ProductId, change.FlightId, null, change.Rollout.SubmissionId), change.Rollout.Action, change.Rollout.Percentage, cancellationToken);

        await _submissionRepository.ExecuteUpdateAsync(s => s.StoreAppId == product.StoreAppId && s.Target == target && s.ExternalId == change.Rollout.SubmissionId,
                                                       new SetPropertyBuilder<MsSubmission>().SetPropertyValue(s => s.RolloutPercentage, (double?)rollout.Percentage)
                                                                                             .SetPropertyValue(s => s.RolloutStatus, (MsRolloutStatus?)rollout.Status),
                                                       cancellationToken);

        await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, $"{rollout.Status} {rollout.Percentage}%", cancellationToken);

        return $"rollout {rollout.Status} at {rollout.Percentage}%.";
    }

    private async Task<string> CreateFlightAsync(IStoreJobContext context, string secret, MsProductContext product, MsFlightInputDto flight, CancellationToken cancellationToken)
    {
        var step = await context.BeginStepAsync("flight.create", StepDirection.Outbound, flight.FriendlyName, cancellationToken: cancellationToken);
        var created = await _microsoftStoreClient.CreateFlightAsync(secret, product.ProductId, flight.FriendlyName.Trim(), flight.GroupIds, flight.RankHigherThan, cancellationToken);

        await _flightRepository.AddAsync(new MsFlight
        {
            OrganizationId = context.OrganizationId,
            StoreAppId = product.StoreAppId,
            FlightId = created.FlightId,
            FriendlyName = Trim(created.FriendlyName ?? flight.FriendlyName.Trim(), MsFlight.FriendlyNameMaxLength),
            GroupIds = [.. created.GroupIds.Count > 0 ? created.GroupIds : flight.GroupIds],
            PendingSubmissionId = created.PendingSubmissionId,
            SyncedDate = _timeProvider.GetUtcNow().UtcDateTime,
        }, cancellationToken);

        await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, created.FlightId, cancellationToken);

        return $"flight {created.FlightId} created.";
    }

    private async Task<string> CreateAddonAsync(IStoreJobContext context, string secret, MsProductContext product, MsAddonInputDto addon, CancellationToken cancellationToken)
    {
        var step = await context.BeginStepAsync("addon.create", StepDirection.Outbound, addon.ProductId, cancellationToken: cancellationToken);
        var created = await _microsoftStoreClient.CreateAddonAsync(secret, product.ProductId, addon.ProductId.Trim(), addon.ProductType.ToString(), cancellationToken);

        await _addonRepository.AddAsync(new MsAddon
        {
            OrganizationId = context.OrganizationId,
            StoreAppId = product.StoreAppId,
            StoreId = created.StoreId,
            ProductId = Trim(created.ProductId ?? addon.ProductId.Trim(), MsAddon.ProductIdMaxLength),
            Type = addon.ProductType,
            PendingSubmissionId = created.PendingSubmissionId,
            SyncedDate = _timeProvider.GetUtcNow().UtcDateTime,
        }, cancellationToken);

        await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, created.StoreId, cancellationToken);

        return $"add-on {created.StoreId} created.";
    }

    private async Task<string> DiscardAsync(IStoreJobContext context, string secret, MsProductContext product, MsChangeInputDto change, MsContentDto content, CancellationToken cancellationToken)
    {
        var target = MsChangePlanner.TargetOf(product.Kind, change);
        var step = await context.BeginStepAsync("submission.delete", StepDirection.Outbound, content.SubmissionId, cancellationToken: cancellationToken);

        await _microsoftStoreClient.DeleteSubmissionAsync(secret, MsJobSupport.Ref(target, product.ProductId, change.FlightId, change.AddonStoreId, content.SubmissionId), cancellationToken);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        await _submissionRepository.UpsertSubmissionAsync(context.OrganizationId, product.StoreAppId, target, content.SubmissionId, row =>
        {
            MsRowWriter.Observe(row, MsSubmissionStatus.Canceled, null, now);
            row.IsPending = false;
            row.FlightId = change.FlightId;
            row.AddonStoreId = change.AddonStoreId;
            row.SyncedDate = now;
        }, cancellationToken);

        await SetPendingAsync(product, target, change.FlightId, change.AddonStoreId, null, cancellationToken);
        await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, null, cancellationToken);

        return $"draft {content.SubmissionId} deleted.";
    }

    private static string Trim(string value, int maxLength) => value?.Length > maxLength ? value[..maxLength] : value;
}
