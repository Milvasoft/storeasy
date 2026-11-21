using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;
using Storeasy.Infrastructure.Stores.Content;
using System.Text.Json;

namespace Storeasy.Infrastructure.Stores.Experiments;

/// <summary>
/// App Store Connect product page optimization (experiments v2) and custom product pages:
/// <list type="bullet">
/// <item>Writing an experiment creates or updates it, its treatments (name, alternate icon) and their localizations, and
/// makes each localization's screenshot and preview sets match the plan through the same reserve, upload and commit
/// protocol as the version's sets. A treatment the plan no longer has is deleted. A create retried after a lost answer
/// finds the experiment or treatment by name instead of making a second one.</item>
/// <item>Writing a custom product page creates it with its first version and localization inline, or makes a new version
/// when the latest one can no longer be edited, then writes the localizations' promotional texts and media.</item>
/// <item>Submitting adds the experiment (or page version) to the open review submission and submits it, unless it goes
/// with the app version; starting and stopping set the experiment's <c>started</c> attribute.</item>
/// <item>Applying a winner writes its media to the app version through the content engine.</item>
/// </list>
/// </summary>
/// <param name="http"></param>
/// <param name="fileStorage"></param>
public sealed class AppleExperimentGateway(StoreHttp http, IFileStorage fileStorage) : IAsoExperimentGateway
{
    private readonly StoreHttp _http = http;
    private readonly AppleContentGateway _content = new(http, fileStorage);

    /// <inheritdoc/>
    public StoreKind Store => StoreKind.Apple;

    /// <inheritdoc/>
    public async Task<StoreAsoRead> ReadAsync(StoreContentTarget target, CancellationToken cancellationToken)
    {
        var api = Api(target);
        var appId = Uri.EscapeDataString(target.ExternalId);
        var read = new StoreAsoRead();

        var (experiments, _) = await api.GetAllAsync($"/v1/apps/{appId}/appStoreVersionExperimentsV2?limit=50", cancellationToken);

        foreach (var resource in experiments)
        {
            var experiment = AppleExperimentPayloads.ParseExperiment(resource);
            var (treatments, _) = await api.GetAllAsync($"/v2/appStoreVersionExperiments/{experiment.Id}/appStoreVersionExperimentTreatments?limit=50", cancellationToken);

            experiment.Treatments = [.. treatments.Select(AppleExperimentPayloads.ParseTreatment)];
            read.Experiments.Add(experiment);
        }

        var (pages, _) = await api.GetAllAsync($"/v1/apps/{appId}/appCustomProductPages?limit=50", cancellationToken);

        foreach (var resource in pages)
        {
            var page = AppleExperimentPayloads.ParsePage(resource);

            await ReadVersionAsync(api, page, cancellationToken);
            read.CustomPages.Add(page);
        }

        return read;
    }

    /// <inheritdoc/>
    public async Task<AsoApplyResult> ApplyAsync(StoreContentTarget target, AsoExperimentChangeSet changes, IStoreJobContext context, CancellationToken cancellationToken)
    {
        var api = Api(target);

        return changes.Operation switch
        {
            AsoExperimentOperation.Push when changes.Kind == AsoExperimentKind.CustomProductPage => await PushPageAsync(api, target.ExternalId, changes, context, cancellationToken),
            AsoExperimentOperation.Push => await PushExperimentAsync(api, target.ExternalId, changes, context, cancellationToken),
            AsoExperimentOperation.Submit => await SubmitAsync(api, target.ExternalId, changes, context, cancellationToken),
            AsoExperimentOperation.Start or AsoExperimentOperation.Stop => await StartStopAsync(api, changes, context, cancellationToken),
            _ => await ApplyWinnerAsync(target, changes, context, cancellationToken),
        };
    }

    private async Task<AsoApplyResult> PushExperimentAsync(AppStoreConnectApi api, string appId, AsoExperimentChangeSet changes, IStoreJobContext context, CancellationToken cancellationToken)
    {
        var result = new AsoApplyResult();

        var experimentId = await ContentSteps.RunAsync(context, "experiment.write", changes.Name, async () =>
        {
            var id = changes.ExternalId ?? await FindExperimentAsync(api, appId, changes.Name, cancellationToken);

            if (id == null)
            {
                using var created = await api.PostAsync("/v2/appStoreVersionExperiments", AppleExperimentPayloads.CreateExperiment(appId, changes.Name, changes.Platform, changes.TrafficProportion ?? 0), cancellationToken);

                return JsonRead.Str(JsonRead.Get(created.RootElement, "data"), "id");
            }

            using var _ = await api.PatchAsync($"/v2/appStoreVersionExperiments/{id}", AppleExperimentPayloads.UpdateExperiment(id, changes.Name, changes.TrafficProportion), cancellationToken);

            return id;
        }, cancellationToken);

        result.ExternalId = experimentId;
        result.Lines.Add($"Experiment {experimentId} written.");

        var (existing, _) = await api.GetAllAsync($"/v2/appStoreVersionExperiments/{experimentId}/appStoreVersionExperimentTreatments?limit=50", cancellationToken);
        var treatments = existing.Select(AppleExperimentPayloads.ParseTreatment).ToList();
        var kept = new HashSet<string>(StringComparer.Ordinal);

        foreach (var change in changes.Treatments)
        {
            var current = treatments.FirstOrDefault(t => t.Id == change.ExternalId) ?? treatments.FirstOrDefault(t => !kept.Contains(t.Id) && t.Name == change.Name);

            var treatmentId = await ContentSteps.RunAsync(context, "treatment.write", change.Name, async () =>
            {
                if (current == null)
                {
                    using var created = await api.PostAsync("/v1/appStoreVersionExperimentTreatments", AppleExperimentPayloads.CreateTreatment(experimentId, change.Name, change.AppIconName), cancellationToken);

                    return JsonRead.Str(JsonRead.Get(created.RootElement, "data"), "id");
                }

                if (current.Name != change.Name || current.AppIconName != change.AppIconName)
                {
                    using var _ = await api.PatchAsync($"/v1/appStoreVersionExperimentTreatments/{current.Id}", AppleExperimentPayloads.UpdateTreatment(current.Id, change.Name, change.AppIconName), cancellationToken);
                }

                return current.Id;
            }, cancellationToken);

            kept.Add(treatmentId);
            result.TreatmentIds[change.Key] = treatmentId;

            if (change.Locales.Count == 0)
                continue;

            var (localizations, _) = await api.GetAllAsync($"/v1/appStoreVersionExperimentTreatments/{treatmentId}/appStoreVersionExperimentTreatmentLocalizations?limit=50", cancellationToken);

            foreach (var locale in change.Locales)
            {
                var localizationId = localizations.Where(l => string.Equals(JsonRead.Attr(l, "locale"), locale.Locale, StringComparison.OrdinalIgnoreCase)).Select(l => JsonRead.Str(l, "id")).FirstOrDefault();

                localizationId ??= await ContentSteps.RunAsync(context, "treatmentLocalization.create", $"{change.Name} {locale.Locale}", async () =>
                {
                    using var created = await api.PostAsync("/v1/appStoreVersionExperimentTreatmentLocalizations", AppleExperimentPayloads.CreateTreatmentLocalization(treatmentId, locale.Locale), cancellationToken);

                    return JsonRead.Str(JsonRead.Get(created.RootElement, "data"), "id");
                }, cancellationToken);

                foreach (var set in locale.Media)
                    result.Lines.Add(await _content.SyncMediaSetAsync(api, AppleMediaParent.TreatmentLocalization(localizationId), set, context, cancellationToken));
            }
        }

        foreach (var removed in treatments.Where(t => !kept.Contains(t.Id)))
        {
            await ContentSteps.RunAsync(context, "treatment.delete", removed.Name, async () =>
            {
                await api.DeleteAsync($"/v1/appStoreVersionExperimentTreatments/{removed.Id}", cancellationToken);
                return null;
            }, cancellationToken);
        }

        result.Lines.Add($"{changes.Treatments.Count} treatments written.");

        return result;
    }

    private async Task<AsoApplyResult> PushPageAsync(AppStoreConnectApi api, string appId, AsoExperimentChangeSet changes, IStoreJobContext context, CancellationToken cancellationToken)
    {
        var result = new AsoApplyResult();
        var change = changes.Treatments.FirstOrDefault() ?? throw new StoreCallException("The plan has no custom product page content.", null, false);
        var first = change.Locales.FirstOrDefault() ?? throw new StoreCallException("The custom product page needs a localization.", null, false);

        var pageId = await ContentSteps.RunAsync(context, "page.write", changes.Name, async () =>
        {
            var id = changes.ExternalId ?? await FindPageAsync(api, appId, changes.Name, cancellationToken);

            if (id == null)
            {
                using var created = await api.PostAsync("/v1/appCustomProductPages", AppleExperimentPayloads.CreatePage(appId, changes.Name, first.Locale, first.PromotionalText), cancellationToken);

                return JsonRead.Str(JsonRead.Get(created.RootElement, "data"), "id");
            }

            using var _ = await api.PatchAsync($"/v1/appCustomProductPages/{id}", AppleExperimentPayloads.UpdatePage(id, changes.Name), cancellationToken);

            return id;
        }, cancellationToken);

        result.ExternalId = pageId;

        var page = new StoreCustomPageState { Id = pageId };

        await ReadVersionAsync(api, page, cancellationToken);

        var versionId = page.VersionId;

        if (versionId == null || !AppleExperimentPayloads.EditablePageStates.Contains(page.VersionState ?? string.Empty))
        {
            versionId = await ContentSteps.RunAsync(context, "pageVersion.create", changes.Name, async () =>
            {
                using var created = await api.PostAsync("/v1/appCustomProductPageVersions", AppleExperimentPayloads.CreatePageVersion(pageId), cancellationToken);

                return JsonRead.Str(JsonRead.Get(created.RootElement, "data"), "id");
            }, cancellationToken);

            page.Localizations = [.. (await api.GetAllAsync($"/v1/appCustomProductPageVersions/{versionId}/appCustomProductPageLocalizations?limit=50", cancellationToken)).Data.Select(AppleExperimentPayloads.ParsePageLocalization)];
        }

        result.ExternalVersionId = versionId;

        foreach (var locale in change.Locales)
        {
            var current = page.Localizations.FirstOrDefault(l => string.Equals(l.Locale, locale.Locale, StringComparison.OrdinalIgnoreCase));

            var localizationId = await ContentSteps.RunAsync(context, "pageLocalization.write", locale.Locale, async () =>
            {
                if (current == null)
                {
                    using var created = await api.PostAsync("/v1/appCustomProductPageLocalizations", AppleExperimentPayloads.CreatePageLocalization(versionId, locale.Locale, locale.PromotionalText), cancellationToken);

                    return JsonRead.Str(JsonRead.Get(created.RootElement, "data"), "id");
                }

                if (locale.PromotionalText != null && locale.PromotionalText != current.PromotionalText)
                {
                    using var _ = await api.PatchAsync($"/v1/appCustomProductPageLocalizations/{current.Id}", AppleExperimentPayloads.UpdatePageLocalization(current.Id, locale.PromotionalText), cancellationToken);
                }

                return current.Id;
            }, cancellationToken);

            foreach (var set in locale.Media)
                result.Lines.Add(await _content.SyncMediaSetAsync(api, AppleMediaParent.CustomPageLocalization(localizationId), set, context, cancellationToken));
        }

        using (var read = await api.GetAsync($"/v1/appCustomProductPages/{pageId}", cancellationToken))
            result.Url = JsonRead.Attr(JsonRead.Get(read.RootElement, "data"), "url");

        result.State = page.VersionState;
        result.Lines.Add($"Custom product page {pageId} version {versionId} written.");

        return result;
    }

    private static async Task<AsoApplyResult> SubmitAsync(AppStoreConnectApi api, string appId, AsoExperimentChangeSet changes, IStoreJobContext context, CancellationToken cancellationToken)
    {
        var itemId = changes.Kind == AsoExperimentKind.CustomProductPage ? changes.ExternalVersionId : changes.ExternalId;

        if (itemId == null && changes.Kind == AsoExperimentKind.CustomProductPage && changes.ExternalId != null)
        {
            var page = new StoreCustomPageState { Id = changes.ExternalId };

            await ReadVersionAsync(api, page, cancellationToken);
            itemId = page.VersionId;
        }

        if (itemId == null)
            throw new StoreCallException("There is nothing in the store to submit yet.", null, false);

        var outcome = await ContentSteps.RunAsync(context, "reviewSubmission.submit", itemId, async () =>
        {
            var (open, _) = await api.GetAllAsync($"/v1/reviewSubmissions?filter[app]={Uri.EscapeDataString(appId)}&filter[state]=READY_FOR_REVIEW&filter[platform]=IOS&limit=5", cancellationToken);
            var submissionId = open.Select(s => JsonRead.Str(s, "id")).FirstOrDefault();

            if (submissionId == null)
            {
                using var created = await api.PostAsync("/v1/reviewSubmissions", new
                {
                    data = new { type = "reviewSubmissions", attributes = new { platform = "IOS" }, relationships = new { app = new { data = new { type = "apps", id = appId } } } },
                }, cancellationToken);

                submissionId = JsonRead.Str(JsonRead.Get(created.RootElement, "data"), "id");
            }

            try
            {
                using var _ = await api.PostAsync("/v1/reviewSubmissionItems", AppleExperimentPayloads.SubmissionItem(submissionId, changes.Kind, itemId), cancellationToken);
            }
            catch (StoreCallException ex) when (ex.StatusCode == 409)
            {
                // Already an item of the open submission (a retried job).
            }

            if (changes.WithVersion)
                return "Added to the review submission; it goes to App Review with the app version.";

            using var submitted = await api.PatchAsync($"/v1/reviewSubmissions/{submissionId}", new
            {
                data = new { type = "reviewSubmissions", id = submissionId, attributes = new { submitted = true } },
            }, cancellationToken);

            return $"Submitted for review ({JsonRead.Attr(JsonRead.Get(submitted.RootElement, "data"), "state") ?? "submitted"}).";
        }, cancellationToken);

        return new AsoApplyResult { Lines = [outcome], ExternalId = changes.ExternalId, ExternalVersionId = changes.Kind == AsoExperimentKind.CustomProductPage ? itemId : null };
    }

    private static async Task<AsoApplyResult> StartStopAsync(AppStoreConnectApi api, AsoExperimentChangeSet changes, IStoreJobContext context, CancellationToken cancellationToken)
    {
        var start = changes.Operation == AsoExperimentOperation.Start;
        var id = changes.ExternalId ?? throw new StoreCallException("The experiment is not in the store.", null, false);

        var state = await ContentSteps.RunAsync(context, start ? "experiment.start" : "experiment.stop", changes.Name, async () =>
        {
            using var updated = await api.PatchAsync($"/v2/appStoreVersionExperiments/{id}", AppleExperimentPayloads.UpdateExperiment(id, started: start), cancellationToken);

            return JsonRead.Attr(JsonRead.Get(updated.RootElement, "data"), "state") ?? (start ? "started" : "stopped");
        }, cancellationToken);

        return new AsoApplyResult { Lines = [$"Experiment {id} {(start ? "started" : "stopped")} ({state})."], ExternalId = id, State = state };
    }

    private async Task<AsoApplyResult> ApplyWinnerAsync(StoreContentTarget target, AsoExperimentChangeSet changes, IStoreJobContext context, CancellationToken cancellationToken)
    {
        var versionId = changes.VersionId ?? throw new StoreCallException("The change needs an App Store version and none was given.", null, false);
        var lines = await _content.ApplyAsync(target with { VersionId = versionId }, new ContentChangeSet { VersionId = versionId, Media = changes.VersionMedia }, context, cancellationToken);

        return new AsoApplyResult { Lines = [.. lines], ExternalId = changes.ExternalId };
    }

    private static async Task ReadVersionAsync(AppStoreConnectApi api, StoreCustomPageState page, CancellationToken cancellationToken)
    {
        var (versions, _) = await api.GetAllAsync($"/v1/appCustomProductPages/{page.Id}/appCustomProductPageVersions?limit=50", cancellationToken);
        var latest = AppleExperimentPayloads.LatestVersion(versions);

        if (latest.ValueKind != JsonValueKind.Object)
            return;

        page.VersionId = JsonRead.Str(latest, "id");
        page.VersionState = JsonRead.Attr(latest, "state");

        var (localizations, _) = await api.GetAllAsync($"/v1/appCustomProductPageVersions/{page.VersionId}/appCustomProductPageLocalizations?limit=50", cancellationToken);

        page.Localizations = [.. localizations.Select(AppleExperimentPayloads.ParsePageLocalization)];
    }

    private static async Task<string> FindExperimentAsync(AppStoreConnectApi api, string appId, string name, CancellationToken cancellationToken)
    {
        var (experiments, _) = await api.GetAllAsync($"/v1/apps/{Uri.EscapeDataString(appId)}/appStoreVersionExperimentsV2?limit=50", cancellationToken);

        return experiments.Where(e => JsonRead.Attr(e, "name") == name && JsonRead.Attr(e, "state") == "PREPARE_FOR_SUBMISSION").Select(e => JsonRead.Str(e, "id")).FirstOrDefault();
    }

    private static async Task<string> FindPageAsync(AppStoreConnectApi api, string appId, string name, CancellationToken cancellationToken)
    {
        var (pages, _) = await api.GetAllAsync($"/v1/apps/{Uri.EscapeDataString(appId)}/appCustomProductPages?limit=50", cancellationToken);

        return pages.Where(p => JsonRead.Attr(p, "name") == name).Select(p => JsonRead.Str(p, "id")).FirstOrDefault();
    }

    private AppStoreConnectApi Api(StoreContentTarget target)
    {
        var credentials = StoreCredentialParser.ParseApple(target.SecretJson, out var errorKey) ?? throw new StoreCallException(errorKey, null, false);

        return new AppStoreConnectApi(_http, credentials);
    }
}
