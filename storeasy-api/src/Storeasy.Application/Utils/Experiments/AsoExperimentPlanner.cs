using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Content;
using Storeasy.Domain.Experiments;
using Storeasy.Domain.Rules;

namespace Storeasy.Application.Utils.Experiments;

/// <summary>
/// What the planner works from: the record (already merged with the store read), the operation, the store read, the
/// uploaded files the variants use and, for applying a winner, the editable app version's media.
/// </summary>
/// <param name="App">Store record.</param>
/// <param name="Experiment">The record.</param>
/// <param name="Operation">What to plan.</param>
/// <param name="WithVersion">Submit: with the app version instead of alone.</param>
/// <param name="WinnerVariantKey">Apply winner: the variant (null: the recorded winner).</param>
/// <param name="Read">Store read.</param>
/// <param name="Files">Inspected assets of the variants (or of the winner).</param>
/// <param name="Version">Apply winner: the app version's content with its media; null otherwise.</param>
/// <param name="Catalog">Rules catalog (media checks).</param>
/// <param name="Localizer">Localizer of the request.</param>
public sealed record AsoPlanInput(StoreAppContext App,
                                  AsoExperiment Experiment,
                                  AsoExperimentOperation Operation,
                                  bool WithVersion,
                                  string WinnerVariantKey,
                                  StoreAsoRead Read,
                                  IReadOnlyList<InspectedAsset> Files,
                                  StoreContentSnapshot Version,
                                  RuleCatalog Catalog,
                                  IMilvaLocalizer Localizer);

/// <summary>
/// A plan draft, or the message key (and detail) of why there is none.
/// </summary>
/// <param name="Draft">The draft.</param>
/// <param name="ErrorKey">Message key.</param>
/// <param name="ErrorDetail">Detail for the message.</param>
public sealed record AsoPlanResult(PlanDraft Draft, string ErrorKey = null, string ErrorDetail = null)
{
    /// <summary>
    /// Whether a draft was made.
    /// </summary>
    public bool IsSuccess => this.ErrorKey == null;
}

/// <summary>
/// Plans the store writes of an App Store experiment or custom product page (<see cref="ChangePlanKind.AsoExperiment"/>):
/// the field level diff against the store read, the calls of the apply job and the change set, with the permission and
/// typed confirmation of the operation (<see cref="AsoExperimentRules"/>). Every file a variant uses is checked against
/// the catalog rule of its class. Pure: no database, no store.
/// </summary>
public static class AsoExperimentPlanner
{
    /// <summary>
    /// Plan group of experiment wide values.
    /// </summary>
    public const string ExperimentGroup = "experiment";

    /// <summary>
    /// Builds the draft of <paramref name="input"/>.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static AsoPlanResult Build(AsoPlanInput input)
    {
        var experiment = input.Experiment;

        if (!AsoExperimentRules.IsApple(experiment.Kind) || input.App.Store != StoreKind.Apple)
            return new AsoPlanResult(null, MessageKey.AsoExperimentNoApi);

        var winner = input.WinnerVariantKey ?? experiment.WinnerVariantKey;
        var allowed = AsoExperimentRules.Operations(experiment.Kind, experiment.Status, experiment.ExternalId != null, experiment.ReviewRequired, winner != null);

        if (!allowed.Contains(input.Operation))
            return new AsoPlanResult(null, MessageKey.AsoExperimentOperationNotAllowed, $"{input.Operation} / {experiment.Status}");

        var draft = new PlanDraft
        {
            Kind = ChangePlanKind.AsoExperiment,
            RequiredPermission = AsoExperimentRules.PermissionOf(input.Operation),
            ConfirmText = AsoExperimentRules.NeedsConfirmation(input.Operation) ? AsoExperimentRules.ConfirmationText(input.App.AppName, experiment.Name) : null,
            BaseKeys = AsoExperimentContent.BaseKeysOf(experiment.Kind, experiment.ExternalId),
        };

        var changes = new AsoExperimentChangeSet
        {
            ExperimentId = experiment.Id,
            Kind = experiment.Kind,
            Operation = input.Operation,
            ExternalId = experiment.ExternalId,
            ExternalVersionId = experiment.ExternalVersionId,
            Name = experiment.Name,
            TrafficProportion = experiment.TrafficProportion,
            WithVersion = input.WithVersion,
        };

        draft.CustomChanges = changes;

        var label = experiment.Kind == AsoExperimentKind.CustomProductPage ? "custom product page" : "experiment";
        string error, detail;

        switch (input.Operation)
        {
            case AsoExperimentOperation.Push:
                (error, detail) = experiment.Kind == AsoExperimentKind.CustomProductPage ? PlanCustomPage(input, draft, changes) : PlanExperiment(input, draft, changes);
                draft.Title = $"{(experiment.ExternalId == null ? "Create" : "Update")} {label} '{experiment.Name}'";
                break;
            case AsoExperimentOperation.Submit:
                (error, detail) = (null, null);
                draft.Items.Add(Item(ExperimentGroup, null, "review", experiment.StoreState, input.WithVersion ? "with the app version" : "submitted", ChangeAction.Update));
                draft.Calls.Add(new PlanCall { Method = "POST", Path = "/v1/reviewSubmissionItems", Target = experiment.ExternalVersionId ?? experiment.ExternalId });

                if (!input.WithVersion)
                    draft.Calls.Add(new PlanCall { Method = "PATCH", Path = "/v1/reviewSubmissions/{id}", Target = "submitted" });

                draft.Title = $"Submit {label} '{experiment.Name}' for review{(input.WithVersion ? " with the app version" : string.Empty)}";
                break;
            case AsoExperimentOperation.Start:
            case AsoExperimentOperation.Stop:
                (error, detail) = (null, null);
                var start = input.Operation == AsoExperimentOperation.Start;
                draft.Items.Add(Item(ExperimentGroup, null, "started", start ? "false" : "true", start ? "true" : "false", ChangeAction.Update));
                draft.Calls.Add(new PlanCall { Method = "PATCH", Path = $"/v2/appStoreVersionExperiments/{experiment.ExternalId}", Target = start ? "started=true" : "started=false" });
                draft.Title = $"{(start ? "Start" : "Stop")} experiment '{experiment.Name}'";
                break;
            default:
                (error, detail) = PlanWinner(input, draft, changes, winner);
                draft.Title = $"Apply winner {winner} of '{experiment.Name}' to version {input.Version?.VersionString}";
                break;
        }

        if (error != null)
            return new AsoPlanResult(null, error, detail);

        draft.Title = Trim(draft.Title, ChangePlan.TitleMaxLength);

        return draft.Items.Count == 0 ? new AsoPlanResult(null, MessageKey.PlanNoChanges) : new AsoPlanResult(draft);
    }

    /// <summary>
    /// Checks the variants of a record before it is saved: count, names, texts within the store limits and the traffic
    /// proportion. Returns the message key and detail of the first problem, or null.
    /// </summary>
    /// <param name="kind"></param>
    /// <param name="trafficProportion"></param>
    /// <param name="variants"></param>
    /// <returns></returns>
    public static (string Key, string Detail) Validate(AsoExperimentKind kind, int? trafficProportion, IReadOnlyList<AsoExperimentVariant> variants)
    {
        if (variants == null || variants.Count == 0)
            return (MessageKey.AsoVariantsRequired, null);

        var max = AsoExperimentRules.MaxVariants(kind);

        if (variants.Count > max)
            return (MessageKey.AsoTooManyVariants, max.ToString(System.Globalization.CultureInfo.InvariantCulture));

        if (AsoExperimentRules.HasTraffic(kind) && trafficProportion is not (>= 1 and <= 100))
            return (MessageKey.AsoTrafficInvalid, null);

        if (variants.Select(v => v.Key).Distinct(StringComparer.Ordinal).Count() != variants.Count || variants.Any(v => string.IsNullOrWhiteSpace(v.Key) || v.Key.Length > 20))
            return (MessageKey.AsoVariantKeyInvalid, null);

        foreach (var variant in variants)
        {
            if (string.IsNullOrWhiteSpace(variant.Name) || variant.Name.Length > AsoExperimentRules.VariantNameMaxLength)
                return (MessageKey.AsoVariantNameInvalid, variant.Key);

            if (variant.AppIconName?.Length > AsoExperimentRules.IconNameMaxLength)
                return (MessageKey.AsoVariantNameInvalid, variant.Key);

            var locales = variant.Locales ?? [];

            if (locales.Select(l => l.Locale).Distinct(StringComparer.OrdinalIgnoreCase).Count() != locales.Count || locales.Any(l => string.IsNullOrWhiteSpace(l.Locale)))
                return (MessageKey.AsoLocaleInvalid, variant.Key);

            foreach (var locale in locales)
            {
                if (locale.PromotionalText?.Length > AsoExperimentRules.PromotionalTextMaxLength)
                    return (MessageKey.AsoTextTooLong, $"{variant.Key} {locale.Locale} promotionalText {AsoExperimentRules.PromotionalTextMaxLength}");

                if (locale.ShortDescription?.Length > AsoExperimentRules.ShortDescriptionMaxLength)
                    return (MessageKey.AsoTextTooLong, $"{variant.Key} {locale.Locale} shortDescription {AsoExperimentRules.ShortDescriptionMaxLength}");

                if (locale.FullDescription?.Length > AsoExperimentRules.FullDescriptionMaxLength)
                    return (MessageKey.AsoTextTooLong, $"{variant.Key} {locale.Locale} fullDescription {AsoExperimentRules.FullDescriptionMaxLength}");

                foreach (var media in locale.Media ?? [])
                {
                    var store = AsoExperimentRules.IsApple(kind) ? StoreKind.Apple : StoreKind.Google;

                    if (MediaClasses.Find(store, media.Kind, media.ClassKey) == null)
                        return (MessageKey.MediaClassUnknown, media.ClassKey);
                }
            }
        }

        return (null, null);
    }

    /// <summary>
    /// Asset ids a record's variants use.
    /// </summary>
    /// <param name="variants"></param>
    /// <returns></returns>
    public static List<long> AssetIdsOf(IEnumerable<AsoExperimentVariant> variants)
        => [.. variants.SelectMany(v => v.Locales ?? []).SelectMany(l => l.Media ?? []).SelectMany(m => m.AssetIds ?? []).Distinct()];

    private static (string, string) PlanExperiment(AsoPlanInput input, PlanDraft draft, AsoExperimentChangeSet changes)
    {
        var experiment = input.Experiment;
        var store = experiment.ExternalId == null ? null : input.Read.Experiments.FirstOrDefault(e => e.Id == experiment.ExternalId);

        if (experiment.ExternalId != null && store == null)
            return (MessageKey.AsoExperimentNotInStore, experiment.ExternalId);

        var create = store == null;

        if (create || store.Name != experiment.Name)
            draft.Items.Add(Item(ExperimentGroup, null, "name", store?.Name, experiment.Name, create ? ChangeAction.Create : ChangeAction.Update));

        if (create || store.TrafficProportion != experiment.TrafficProportion)
            draft.Items.Add(Item(ExperimentGroup, null, "trafficProportion", Text(store?.TrafficProportion), Text(experiment.TrafficProportion), create ? ChangeAction.Create : ChangeAction.Update));

        draft.Calls.Add(create
            ? new PlanCall { Method = "POST", Path = "/v2/appStoreVersionExperiments", Target = experiment.Name }
            : new PlanCall { Method = "PATCH", Path = $"/v2/appStoreVersionExperiments/{store.Id}", Target = experiment.Name });

        foreach (var variant in experiment.Variants)
        {
            if (string.IsNullOrWhiteSpace(variant.AppIconName) && !(variant.Locales ?? []).Any(l => l.Media?.Any(m => m.AssetIds?.Count > 0) == true))
                return (MessageKey.AsoVariantEmpty, variant.Key);

            var treatment = store?.Treatments.FirstOrDefault(t => t.Id == variant.ExternalId);
            var group = $"treatment {variant.Key}";
            var change = new AsoTreatmentChange { Key = variant.Key, Name = variant.Name, AppIconName = Blank(variant.AppIconName), ExternalId = treatment?.Id };

            if (treatment == null || treatment.Name != variant.Name)
                draft.Items.Add(Item(group, null, "name", treatment?.Name, variant.Name, treatment == null ? ChangeAction.Create : ChangeAction.Update));

            if (treatment == null ? change.AppIconName != null : treatment.AppIconName != change.AppIconName)
                draft.Items.Add(Item(group, null, "appIconName", treatment?.AppIconName, change.AppIconName, treatment == null ? ChangeAction.Create : ChangeAction.Update));

            var problem = AddMedia(input, draft, change, variant, group);

            if (problem.Item1 != null)
                return problem;

            draft.Calls.Add(new PlanCall { Method = treatment == null ? "POST" : "PATCH", Path = "/v1/appStoreVersionExperimentTreatments", Target = variant.Name });
            changes.Treatments.Add(change);
        }

        foreach (var removed in store?.Treatments.Where(t => !experiment.Variants.Any(v => v.ExternalId == t.Id)) ?? [])
        {
            draft.Items.Add(Item($"treatment {removed.Name}", null, "treatment", removed.Name, null, ChangeAction.Delete));
            draft.Calls.Add(new PlanCall { Method = "DELETE", Path = $"/v1/appStoreVersionExperimentTreatments/{removed.Id}", Target = removed.Name });
        }

        return (null, null);
    }

    private static (string, string) PlanCustomPage(AsoPlanInput input, PlanDraft draft, AsoExperimentChangeSet changes)
    {
        var experiment = input.Experiment;
        var store = experiment.ExternalId == null ? null : input.Read.CustomPages.FirstOrDefault(p => p.Id == experiment.ExternalId);

        if (experiment.ExternalId != null && store == null)
            return (MessageKey.AsoExperimentNotInStore, experiment.ExternalId);

        var variant = experiment.Variants.FirstOrDefault();

        if (variant == null || (variant.Locales ?? []).Count == 0)
            return (MessageKey.AsoVariantEmpty, variant?.Key);

        var create = store == null;

        if (create || store.Name != experiment.Name)
            draft.Items.Add(Item(ExperimentGroup, null, "name", store?.Name, experiment.Name, create ? ChangeAction.Create : ChangeAction.Update));

        draft.Calls.Add(create
            ? new PlanCall { Method = "POST", Path = "/v1/appCustomProductPages", Target = experiment.Name }
            : new PlanCall { Method = "PATCH", Path = $"/v1/appCustomProductPages/{store.Id}", Target = experiment.Name });

        if (store != null && store.VersionState is not ("PREPARE_FOR_SUBMISSION" or "REJECTED"))
            draft.Calls.Add(new PlanCall { Method = "POST", Path = "/v1/appCustomProductPageVersions", Target = experiment.Name });

        var change = new AsoTreatmentChange { Key = variant.Key, Name = experiment.Name, ExternalId = store?.VersionId };

        foreach (var locale in variant.Locales)
        {
            var current = store?.Localizations.FirstOrDefault(l => string.Equals(l.Locale, locale.Locale, StringComparison.OrdinalIgnoreCase));
            var text = Blank(locale.PromotionalText);

            if (text != null && text != current?.PromotionalText)
                draft.Items.Add(Item("page", locale.Locale, "promotionalText", current?.PromotionalText, text, current == null ? ChangeAction.Create : ChangeAction.Update));
            else if (current == null)
                draft.Items.Add(Item("page", locale.Locale, "localization", null, locale.Locale, ChangeAction.Create));
        }

        var problem = AddMedia(input, draft, change, variant, "page");

        if (problem.Item1 != null)
            return problem;

        // The page's promotional texts go with the localizations of the change.
        foreach (var locale in change.Locales)
            locale.PromotionalText = Blank(variant.Locales.First(l => string.Equals(l.Locale, locale.Locale, StringComparison.OrdinalIgnoreCase)).PromotionalText);

        changes.Treatments.Add(change);

        return (null, null);
    }

    /// <summary>
    /// Adds the variant's locales with their media sets to <paramref name="change"/>, every file checked against its class.
    /// Treatments and new pages have no media in the store yet that storeasy tracks, so every set is written whole (the
    /// gateway keeps items whose checksum is already in the set).
    /// </summary>
    private static (string, string) AddMedia(AsoPlanInput input, PlanDraft draft, AsoTreatmentChange change, AsoExperimentVariant variant, string group)
    {
        foreach (var locale in variant.Locales ?? [])
        {
            var localeChange = new AsoLocaleChange { Locale = locale.Locale };

            foreach (var media in locale.Media ?? [])
            {
                var mediaClass = MediaClasses.Find(StoreKind.Apple, media.Kind, media.ClassKey);

                if (mediaClass == null)
                    return (MessageKey.MediaClassUnknown, media.ClassKey);

                var (set, problem, problemDetail) = MediaSet(input, draft, mediaClass, locale.Locale, media.AssetIds ?? [], []);

                if (problem != null)
                    return (problem, problemDetail);

                if (set.Items.Count == 0)
                    continue;

                localeChange.Media.Add(set);
                draft.Items.Add(Item(group, locale.Locale, $"{media.Kind}:{media.ClassKey}", null, $"{set.Items.Count} files", ChangeAction.Update));
            }

            change.Locales.Add(localeChange);
        }

        return (null, null);
    }

    private static (string, string) PlanWinner(AsoPlanInput input, PlanDraft draft, AsoExperimentChangeSet changes, string winnerKey)
    {
        var variant = input.Experiment.Variants.FirstOrDefault(v => v.Key == winnerKey);

        if (variant == null)
            return (MessageKey.AsoWinnerUnknown, winnerKey);

        var version = input.Version;

        if (version?.VersionId == null || !version.VersionEditable)
            return (MessageKey.VersionNotEditable, null);

        draft.VersionId = version.VersionId;
        draft.VersionString = version.VersionString;
        changes.VersionId = version.VersionId;
        draft.BaseKeys = [];

        if (!string.IsNullOrWhiteSpace(variant.AppIconName))
        {
            draft.Warnings.Add(new PlanWarning
            {
                Key = MessageKey.AsoWinnerIconInBuild,
                Message = input.Localizer[MessageKey.AsoWinnerIconInBuild, variant.AppIconName],
                Field = "appIconName",
            });
        }

        foreach (var locale in variant.Locales ?? [])
        {
            foreach (var media in locale.Media ?? [])
            {
                var mediaClass = MediaClasses.Find(StoreKind.Apple, media.Kind, media.ClassKey);

                if (mediaClass == null)
                    return (MessageKey.MediaClassUnknown, media.ClassKey);

                var existing = version.Media.FirstOrDefault(m => m.Kind == media.Kind && m.ClassKey == media.ClassKey && string.Equals(m.Locale, locale.Locale, StringComparison.OrdinalIgnoreCase));
                var (set, problem, problemDetail) = MediaSet(input, draft, mediaClass, locale.Locale, media.AssetIds ?? [], []);

                if (problem != null)
                    return (problem, problemDetail);

                if (set.Items.Count == 0)
                    continue;

                var before = existing == null ? null : string.Join(",", existing.Items.Select(i => i.Checksum ?? i.Id));
                var after = string.Join(",", set.Items.Select(i => i.Md5));

                if (before == after)
                    continue;

                changes.VersionMedia.Add(set);
                draft.BaseKeys.Add(ContentSnapshots.MediaKey(media.Kind, locale.Locale, media.ClassKey));
                draft.Items.Add(Item(ContentGroups.Media, locale.Locale, $"{media.Kind}:{media.ClassKey}", existing == null ? null : $"{existing.Items.Count} files", $"{set.Items.Count} files", existing == null ? ChangeAction.Create : ChangeAction.Update));
                draft.Calls.Add(new PlanCall { Method = "PATCH", Path = $"/v1/{(media.Kind == MediaKind.Screenshot ? "appScreenshotSets" : "appPreviewSets")}", Target = $"{locale.Locale} {media.ClassKey}" });
            }
        }

        if (changes.VersionMedia.Count == 0)
            return (MessageKey.AsoWinnerNothingToApply, winnerKey);

        return (null, null);
    }

    private static (MediaSetChange Set, string ErrorKey, string Detail) MediaSet(AsoPlanInput input, PlanDraft draft, MediaClass mediaClass, string locale, IReadOnlyList<long> assetIds, IEnumerable<string> existingChecksums)
    {
        var set = new MediaSetChange { Kind = mediaClass.Kind, Locale = locale, ClassKey = mediaClass.ClassKey };

        foreach (var assetId in assetIds)
        {
            var file = input.Files.FirstOrDefault(f => f.AssetId == assetId);

            if (file == null)
                return (null, MessageKey.AssetNotFound, assetId.ToString(System.Globalization.CultureInfo.InvariantCulture));

            var check = MediaPlanner.Check(StoreKind.Apple, mediaClass, file, existingChecksums, input.Catalog, input.Localizer);

            draft.Files.Add(check);

            if (check.Result == MediaPlanner.Rejected)
                return (null, MessageKey.MediaRejected, $"{file.FileName}: {check.Reason}");

            set.Items.Add(new MediaItemChange { AssetId = file.AssetId, ObjectKey = file.ObjectKey, FileName = file.FileName, Size = file.Size, ContentType = file.ContentType, Md5 = file.Md5, Sha256 = file.Sha256 });
        }

        return (set, null, null);
    }

    private static PlanItem Item(string group, string locale, string field, string before, string after, ChangeAction action)
        => new() { Group = group, Locale = locale, Field = field, Before = before, After = after, Action = action };

    private static string Text(int? value) => value?.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Trim(string value, int max) => value == null || value.Length <= max ? value : value[..max];
}
