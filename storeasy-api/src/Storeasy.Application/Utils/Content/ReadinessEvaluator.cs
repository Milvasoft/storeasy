using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ReadinessDtos;
using Storeasy.Domain.Content;
using Storeasy.Domain.Rules;
using System.Globalization;

namespace Storeasy.Application.Utils.Content;

/// <summary>
/// The readiness check (docs/urun-plani.md 11.1, first row): from one live read of the store record, the rules catalog,
/// the app profile, the saved answers and the assisted steps it lists what blocks a submission. Pure: no I/O.
/// </summary>
/// <param name="App">Store record.</param>
/// <param name="Snapshot">Store content (every part).</param>
/// <param name="Subject">Catalog subject of the record.</param>
/// <param name="Catalog">Rules catalog.</param>
/// <param name="Answers">Saved answer documents the app has.</param>
/// <param name="Steps">Assisted step state by key, with the step id.</param>
/// <param name="Today">Day of the check.</param>
/// <param name="Language">Language of the texts.</param>
/// <param name="Localizer">Localizer of the texts.</param>
public sealed record ReadinessInput(StoreAppContext App,
                                    StoreContentSnapshot Snapshot,
                                    RuleSubject Subject,
                                    RuleCatalog Catalog,
                                    IReadOnlySet<SavedAnswerKind> Answers,
                                    IReadOnlyDictionary<string, (int Id, AssistedStepStatus Status)> Steps,
                                    DateOnly Today,
                                    string Language,
                                    IMilvaLocalizer Localizer);

/// <summary>
/// See <see cref="ReadinessInput"/>.
/// </summary>
public static class ReadinessEvaluator
{
    /// <summary>
    /// Every check of the record.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static List<ReadinessItemDto> Evaluate(ReadinessInput input)
    {
        var items = input.App.Store == StoreKind.Apple ? Apple(input) : Google(input);

        items.AddRange(Assisted(input));
        items.AddRange(Deadlines(input, items));

        return items;
    }

    private static List<ReadinessItemDto> Apple(ReadinessInput input)
    {
        var (snapshot, l) = (input.Snapshot, input.Localizer);
        var items = new List<ReadinessItemDto>();

        if (snapshot.VersionId == null)
        {
            items.Add(Item("version", "Version", ReadinessStatus.Missing, l[MessageKey.ReadinessVersion], l[MessageKey.ReadinessVersionMissing]));
            return items;
        }

        var required = ListingFields.Of(StoreKind.Apple).Where(f => f.Localized && (f.Required || (f.Field == "whatsNew" && snapshot.HasLiveVersion == true))).ToList();

        items.AddRange(Texts(input, required, StoreKind.Apple));
        items.Add(Media(input, snapshot.PrimaryLocale ?? snapshot.Locales.FirstOrDefault(), "screenshots"));

        var build = snapshot.Build;

        items.Add(build == null
            ? Item("build", "Build", ReadinessStatus.Missing, l[MessageKey.ReadinessBuild], l[MessageKey.ReadinessBuildMissing], fixRoute: "release/builds")
            : build.ProcessingState != BuildProcessingState.Valid
                ? Item("build", "Build", ReadinessStatus.Missing, l[MessageKey.ReadinessBuild], l[MessageKey.ReadinessBuildNotValid, build.ProcessingState], fixRoute: "release/builds")
                : Item("build", "Build", ReadinessStatus.Ok, l[MessageKey.ReadinessBuild], build.BuildNumber));

        items.Add(build?.UsesNonExemptEncryption == null
            ? Item("export-compliance", "Build", build == null ? ReadinessStatus.Unknown : ReadinessStatus.Missing, l[MessageKey.ReadinessExportCompliance], l[MessageKey.ReadinessExportComplianceMissing], fixRoute: "release/builds")
            : Item("export-compliance", "Build", ReadinessStatus.Ok, l[MessageKey.ReadinessExportCompliance], null));

        var reviewMissing = new[] { "contactFirstName", "contactLastName", "contactEmail", "contactPhone" }.Where(f => Empty(snapshot, ContentGroups.Review, null, f)).ToList();

        if (snapshot.Get(ContentKey.Of(ContentGroups.Review, null, "demoAccountRequired")) == "true")
        {
            if (Empty(snapshot, ContentGroups.Review, null, "demoAccountName"))
                reviewMissing.Add("demoAccountName");

            if (!snapshot.StoreDemoPasswordSet)
                reviewMissing.Add("demoAccountPassword");
        }

        items.Add(Item("review-info", "Review", reviewMissing.Count > 0 ? ReadinessStatus.Missing : ReadinessStatus.Ok, l[MessageKey.ReadinessReviewInfo],
                       reviewMissing.Count > 0 ? l[MessageKey.ReadinessMissingFields, string.Join(", ", reviewMissing)] : null, fixRoute: "release/review-info"));

        var ageRating = snapshot.Values.Where(v => v.Key.StartsWith(ContentGroups.AgeRating + "|", StringComparison.Ordinal)).ToList();
        var unanswered = ageRating.Where(v => string.IsNullOrEmpty(v.Value)).Select(v => ContentKey.Parse(v.Key).Field).ToList();

        items.Add(Item("age-rating", "AgeRating", ageRating.Count == 0 || unanswered.Count > 0 ? ReadinessStatus.Missing : ReadinessStatus.Ok, l[MessageKey.ReadinessAgeRating],
                       unanswered.Count > 0 ? l[MessageKey.ReadinessUnanswered, string.Join(", ", unanswered)] : ageRating.Count == 0 ? l[MessageKey.ReadinessValueMissing] : null,
                       fixRoute: "listing/age-rating"));

        items.Add(Value(input, "category", "Categories", ContentKey.Of(ContentGroups.AppInfo, null, "primaryCategory"), l[MessageKey.ReadinessCategory], "listing/category"));
        items.Add(Value(input, "copyright", "Categories", ContentKey.Of(ContentGroups.Version, null, "copyright"), l[MessageKey.ReadinessCopyright], "listing/category"));
        items.Add(Value(input, "content-rights", "Categories", ContentKey.Of(ContentGroups.App, null, "contentRightsDeclaration"), l[MessageKey.ReadinessContentRights], "listing/category"));

        var available = snapshot.Values.Any(v => v.Key.StartsWith(ContentGroups.Availability + "|", StringComparison.Ordinal) && v.Value == "true");
        var priced = !Empty(snapshot, ContentGroups.Pricing, null, "pricePoint");

        items.Add(Item("pricing", "Pricing", available && priced ? ReadinessStatus.Ok : ReadinessStatus.Missing, l[MessageKey.ReadinessAvailability],
                       !available ? l[MessageKey.ReadinessAvailabilityMissing] : !priced ? l[MessageKey.ReadinessPriceMissing] : null, fixRoute: "listing/pricing"));

        return items;
    }

    private static List<ReadinessItemDto> Google(ReadinessInput input)
    {
        var (snapshot, l) = (input.Snapshot, input.Localizer);
        var items = new List<ReadinessItemDto>();

        var detailsMissing = new[] { "contactEmail", "defaultLanguage" }.Where(f => Empty(snapshot, ContentGroups.Details, null, f)).ToList();

        items.Add(Item("details", "Details", detailsMissing.Count > 0 ? ReadinessStatus.Missing : ReadinessStatus.Ok, l[MessageKey.ReadinessDetails],
                       detailsMissing.Count > 0 ? l[MessageKey.ReadinessMissingFields, string.Join(", ", detailsMissing)] : null, fixRoute: "listing/details"));

        items.AddRange(Texts(input, [.. ListingFields.Of(StoreKind.Google).Where(f => f.Localized && f.Required)], StoreKind.Google));
        items.Add(Media(input, snapshot.PrimaryLocale ?? snapshot.Locales.FirstOrDefault(), "graphics"));

        var hasBundle = snapshot.Tracks.Any(t => t.Releases.Any(r => r.VersionCodes.Count > 0));

        items.Add(Item("bundle", "Build", hasBundle ? ReadinessStatus.Ok : ReadinessStatus.Missing, l[MessageKey.ReadinessBundle], hasBundle ? null : l[MessageKey.ReadinessBundleMissing], fixRoute: "release/builds"));

        foreach (var rule in input.Catalog.ForStore(StoreKind.Google).Where(r => r.Category == "target-api" && r.Params?.MinTargetSdk != null))
        {
            var match = RuleEvaluator.Evaluate(rule, input.Subject);

            if (match.Applicability != RuleApplicability.Applies)
                continue;

            var target = input.App.Profile?.TargetSdk;

            items.Add(target == null
                ? Item($"target-api.{rule.Id}", "TargetApi", ReadinessStatus.Unknown, rule.Title.In(input.Language), l[MessageKey.ReadinessTargetApiUnknown], rule.Id, fixRoute: "settings/profile")
                : Item($"target-api.{rule.Id}", "TargetApi", target >= rule.Params.MinTargetSdk ? ReadinessStatus.Ok : ReadinessStatus.Missing, rule.Title.In(input.Language),
                       target >= rule.Params.MinTargetSdk ? null : l[MessageKey.ReadinessTargetApiLow, target, rule.Params.MinTargetSdk], rule.Id));
        }

        var dataSafety = input.Answers.Contains(SavedAnswerKind.DataSafety);

        items.Add(Item("data-safety", "Answers", dataSafety ? ReadinessStatus.Ok : ReadinessStatus.Missing, l[MessageKey.ReadinessDataSafety],
                       dataSafety ? null : l[MessageKey.ReadinessAnswersMissing], "google.privacy.data-safety", fixRoute: "answers"));

        return items;
    }

    private static IEnumerable<ReadinessItemDto> Texts(ReadinessInput input, List<ListingField> required, StoreKind store)
    {
        var (snapshot, l) = (input.Snapshot, input.Localizer);
        var locales = snapshot.Locales.OrderBy(x => x == snapshot.PrimaryLocale ? 0 : 1).ThenBy(x => x, StringComparer.Ordinal).ToList();

        if (locales.Count == 0)
        {
            yield return Item("texts", "Texts", ReadinessStatus.Missing, l[MessageKey.ReadinessTexts, "-"], l[MessageKey.ReadinessValueMissing], fixRoute: "listing/texts");
            yield break;
        }

        foreach (var locale in locales)
        {
            var missing = required.Where(f => Empty(snapshot, f.Group, locale, f.Field)).Select(f => f.Field).ToList();
            var overLimit = ListingFields.Of(store)
                                         .Where(f => f.Localized)
                                         .Select(f => (f, FieldPlanner.LimitViolation(f, snapshot.Get(ContentKey.Of(f.Group, locale, f.Field)), input.Catalog)))
                                         .Where(x => x.Item2 != null)
                                         .Select(x => $"{x.f.Field} {x.Item2.Value.Actual}/{x.Item2.Value.Limit}")
                                         .ToList();

            var message = string.Join(" ", new[]
            {
                missing.Count > 0 ? (string)l[MessageKey.ReadinessMissingFields, string.Join(", ", missing)] : null,
                overLimit.Count > 0 ? (string)l[MessageKey.ReadinessOverLimit, string.Join(", ", overLimit)] : null,
            }.Where(m => m != null));

            yield return Item($"texts.{locale}",
                              "Texts",
                              missing.Count > 0 || overLimit.Count > 0 ? ReadinessStatus.Missing : ReadinessStatus.Ok,
                              l[MessageKey.ReadinessTexts, locale],
                              message.Length == 0 ? null : message,
                              locale: locale,
                              fixRoute: $"listing/texts?locale={locale}");
        }
    }

    /// <summary>
    /// The media slots of a locale against the catalog: required classes that are neither filled nor derived by the store
    /// from a fallback block the submission (iPad classes count only when the app supports iPad).
    /// </summary>
    private static ReadinessItemDto Media(ReadinessInput input, string locale, string key)
    {
        var l = input.Localizer;
        var store = input.App.Store;
        var classes = MediaClasses.Of(store, MediaKind.Screenshot).ToList();
        var rules = input.Catalog.ForStore(store)
                                 .Where(r => r.Category is "screenshots" or "graphics" && !r.Id.EndsWith(".general", StringComparison.Ordinal))
                                 .Where(r => RuleEvaluator.Evaluate(r, input.Subject).Applicability == RuleApplicability.Applies)
                                 .ToList();

        var counts = rules.ToDictionary(r => r.Id, r => input.Snapshot.Media.Where(m => m.Kind == MediaKind.Screenshot && string.Equals(m.Locale, locale, StringComparison.OrdinalIgnoreCase))
                                                                            .Where(m => classes.Any(c => c.ClassKey == m.ClassKey && c.RuleId == r.Id))
                                                                            .Sum(m => m.Items.Count));

        var slots = MediaSlotEvaluator.Evaluate(rules, counts, input.Today);
        var missing = slots.Where(s => s.Required && s.Fill == MediaSlotFill.None).ToList();
        var title = store == StoreKind.Apple ? l[MessageKey.ReadinessScreenshots, locale ?? "-"] : l[MessageKey.ReadinessGraphics, locale ?? "-"];

        // Play: the store needs a few screenshots in total, whatever the device classes.
        var general = input.Catalog.Find($"{RuleStores.Key(store)}.screenshots.general");
        var total = input.Snapshot.Media.Where(m => string.Equals(m.Locale, locale, StringComparison.OrdinalIgnoreCase) && m.ClassKey != null && m.ClassKey.EndsWith("Screenshots", StringComparison.Ordinal)).Sum(m => m.Items.Count);
        var tooFew = store == StoreKind.Google && general?.Params?.MinCount is { } minCount && total < minCount;

        var names = missing.Select(s => classes.FirstOrDefault(c => c.RuleId == s.RuleId)?.ClassKey ?? s.RuleId).ToList();

        if (tooFew)
            names.Add($"screenshots {total}/{general.Params.MinCount}");

        return new ReadinessItemDto
        {
            Key = key,
            Group = "Media",
            Status = names.Count > 0 ? ReadinessStatus.Missing : ReadinessStatus.Ok,
            Title = title,
            Message = names.Count > 0 ? l[MessageKey.ReadinessMissingClasses, string.Join(", ", names)] : null,
            Locale = locale,
            FixRoute = $"listing/screenshots?locale={locale}",
            Slots = [.. slots.Select(s => new ReadinessSlotDto
            {
                ClassKey = classes.FirstOrDefault(c => c.RuleId == s.RuleId)?.ClassKey,
                RuleId = s.RuleId,
                Required = s.Required,
                FilledBy = s.Fill,
                FallbackFromClassKey = s.FallbackFromRuleId == null ? null : classes.FirstOrDefault(c => c.RuleId == s.FallbackFromRuleId)?.ClassKey ?? s.FallbackFromRuleId,
                Count = s.Count,
            })],
        };
    }

    private static IEnumerable<ReadinessItemDto> Assisted(ReadinessInput input)
    {
        var l = input.Localizer;

        foreach (var step in AssistedStepCatalog.For(input.Catalog, input.Subject))
        {
            if (step.Key == "google.privacy.data-safety")
                continue;

            var rule = step.RuleId == null ? null : input.Catalog.Find(step.RuleId);

            // An assisted rule that starts later is a timeline entry, not a blocker yet.
            if (rule != null && !RuleEvaluator.IsRequiredOn(rule, input.Today))
                continue;

            var done = input.Steps.TryGetValue(step.Key, out var state) && state.Status is AssistedStepStatus.Done or AssistedStepStatus.Skipped;
            var answersMissing = step.AnswerKeys.Where(k => !input.Answers.Contains(k)).ToList();
            var title = rule?.Title.In(input.Language) ?? (string)l[AssistedStepCatalog.TextKey(step, "Title")];

            yield return Item($"assisted.{step.Key}",
                              "Assisted",
                              done ? ReadinessStatus.Ok : ReadinessStatus.Assisted,
                              title,
                              done ? null : answersMissing.Count > 0 ? $"{l[MessageKey.ReadinessStepOpen]} {l[MessageKey.ReadinessAnswersMissing]}" : l[MessageKey.ReadinessStepOpen],
                              step.RuleId,
                              fixRoute: "assisted");
        }
    }

    private static IEnumerable<ReadinessItemDto> Deadlines(ReadinessInput input, List<ReadinessItemDto> existing)
    {
        var covered = existing.Where(i => i.RuleId != null).Select(i => i.RuleId).ToHashSet();
        var timeline = ComplianceTimeline.Evaluate(input.App, input.Subject, input.Catalog, input.Steps, input.Today, input.Language);

        foreach (var item in timeline.Where(i => !covered.Contains(i.RuleId) && i.Status is ComplianceStatus.Missing && i.Applicability == RuleApplicability.Applies))
        {
            var passed = item.DueDate != null && item.DueDate <= input.Today;
            var rule = input.Catalog.Find(item.RuleId);

            if (rule.Check?.Kind == RuleCheck.Assisted)
                continue;

            yield return new ReadinessItemDto
            {
                Key = $"deadline.{item.RuleId}",
                Group = "Compliance",
                Status = passed || RuleEvaluator.IsRequiredOn(rule, input.Today) ? ReadinessStatus.Missing : ReadinessStatus.Warning,
                RuleId = item.RuleId,
                Title = item.Title,
                Message = item.DueDate == null ? item.Summary : input.Localizer[MessageKey.ReadinessDeadline, item.DueDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)],
                DueDate = item.DueDate,
                FixRoute = "compliance",
            };
        }
    }

    private static ReadinessItemDto Value(ReadinessInput input, string key, string group, string contentKey, string title, string fixRoute)
    {
        var empty = string.IsNullOrEmpty(input.Snapshot.Get(contentKey));

        return Item(key, group, empty ? ReadinessStatus.Missing : ReadinessStatus.Ok, title, empty ? input.Localizer[MessageKey.ReadinessValueMissing] : null, fixRoute: fixRoute);
    }

    private static bool Empty(StoreContentSnapshot snapshot, string group, string locale, string field) => string.IsNullOrWhiteSpace(snapshot.Get(ContentKey.Of(group, locale, field)));

    private static ReadinessItemDto Item(string key, string group, ReadinessStatus status, string title, string message, string ruleId = null, string locale = null, string fixRoute = null)
        => new() { Key = key, Group = group, Status = status, Title = title, Message = message, RuleId = ruleId, Locale = locale, FixRoute = fixRoute };
}
