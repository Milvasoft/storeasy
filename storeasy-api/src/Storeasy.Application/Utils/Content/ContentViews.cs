using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.AssistedStepDtos;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Dtos.RuleDtos;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Content;
using Storeasy.Domain.Rules;
using System.Text.Json;

namespace Storeasy.Application.Utils.Content;

/// <summary>
/// Read models built from store snapshots, the rules catalog and assisted step rows. Shared by the read handlers.
/// </summary>
public static class ContentViews
{
    /// <summary>
    /// The listing of a store record, fields of <paramref name="locale"/> (every locale when null).
    /// </summary>
    /// <param name="app"></param>
    /// <param name="snapshot"></param>
    /// <param name="locale"></param>
    /// <param name="catalog"></param>
    /// <returns></returns>
    public static ListingDto Listing(StoreAppContext app, StoreContentSnapshot snapshot, string locale, RuleCatalog catalog)
    {
        var fields = ListingFields.Of(app.Store).ToList();
        var locales = locale == null ? snapshot.Locales : [locale];

        return new ListingDto
        {
            StoreAppId = app.StoreAppId,
            Store = app.Store,
            VersionId = snapshot.VersionId,
            VersionString = snapshot.VersionString,
            VersionState = snapshot.VersionState,
            VersionEditable = snapshot.VersionEditable,
            CatalogVersion = catalog.Version,
            Locales = [.. snapshot.Locales.Select(l => new ListingLocaleDto
            {
                Locale = l,
                IsPrimary = string.Equals(l, snapshot.PrimaryLocale, StringComparison.OrdinalIgnoreCase),
                RequiredCount = fields.Count(f => f.Localized && f.Required),
                FilledCount = fields.Count(f => f.Localized && f.Required && !string.IsNullOrWhiteSpace(snapshot.Get(ContentKey.Of(f.Group, l, f.Field)))),
            })],
            Fields =
            [
                .. locales.SelectMany(l => fields.Where(f => f.Localized).Select(f => Field(f, l, snapshot, catalog))),
                .. fields.Where(f => !f.Localized).Select(f => Field(f, null, snapshot, catalog)),
            ],
        };
    }

    /// <summary>
    /// One listing field as the store has it.
    /// </summary>
    /// <param name="field"></param>
    /// <param name="locale"></param>
    /// <param name="snapshot"></param>
    /// <param name="catalog"></param>
    /// <returns></returns>
    public static ListingFieldDto Field(ListingField field, string locale, StoreContentSnapshot snapshot, RuleCatalog catalog)
    {
        var key = ContentKey.Of(field.Group, locale, field.Field);
        var limits = catalog.Find(field.LimitRuleId)?.Params;

        return new ListingFieldDto
        {
            Key = field.Field,
            Group = field.Group,
            Locale = locale,
            Value = snapshot.Get(key),
            MaxLength = limits?.MaxChars,
            MaxBytes = limits?.MaxBytes,
            Required = field.Required,
            StoreValueHash = ContentDiff.Hash(snapshot.Values, [key])[..16],
        };
    }

    /// <summary>
    /// Media classes of a locale with their catalog requirement and fill state.
    /// </summary>
    /// <param name="app"></param>
    /// <param name="snapshot"></param>
    /// <param name="kind"></param>
    /// <param name="locale"></param>
    /// <param name="subject"></param>
    /// <param name="catalog"></param>
    /// <param name="today"></param>
    /// <param name="language"></param>
    /// <returns></returns>
    public static MediaSetListDto MediaSets(StoreAppContext app, StoreContentSnapshot snapshot, MediaKind kind, string locale, RuleSubject subject, RuleCatalog catalog, DateOnly today, string language)
    {
        var classes = MediaClasses.Of(app.Store, kind).ToList();
        var rules = classes.Select(c => c.RuleId).Distinct().Select(catalog.Find).Where(r => r != null).ToList();
        var applicability = rules.ToDictionary(r => r.Id, r => RuleEvaluator.Evaluate(r, subject).Applicability);
        var applicable = rules.Where(r => applicability[r.Id] == RuleApplicability.Applies).ToList();
        var sets = snapshot.Media.Where(m => m.Kind == kind && string.Equals(m.Locale, locale, StringComparison.OrdinalIgnoreCase)).ToList();
        var counts = applicable.ToDictionary(r => r.Id, r => sets.Where(s => classes.Any(c => c.ClassKey == s.ClassKey && c.RuleId == r.Id)).Sum(s => s.Items.Count));
        var slots = MediaSlotEvaluator.Evaluate(applicable, counts, today).ToDictionary(s => s.RuleId);

        return new MediaSetListDto
        {
            StoreAppId = app.StoreAppId,
            Store = app.Store,
            Kind = kind,
            Locale = locale,
            VersionId = snapshot.VersionId,
            CatalogVersion = catalog.Version,
            Classes = [.. classes.Select(c =>
            {
                var rule = catalog.Find(c.RuleId);
                var set = sets.FirstOrDefault(s => s.ClassKey == c.ClassKey);
                var slot = slots.GetValueOrDefault(c.RuleId);

                return new MediaClassDto
                {
                    ClassKey = c.ClassKey,
                    RuleId = c.RuleId,
                    Title = rule?.Title?.In(language) ?? c.ClassKey,
                    Sizes = rule?.Params?.Sizes ?? [],
                    Requirement = rule?.Requirement?.Level,
                    Required = slot?.Required ?? false,
                    Applicability = applicability.GetValueOrDefault(c.RuleId, RuleApplicability.Unknown),
                    Condition = rule?.Requirement?.Condition ?? rule?.AppliesWhen?.Expression,
                    Fill = set?.Items.Count > 0 ? MediaSlotFill.Own : slot?.Fill ?? MediaSlotFill.None,
                    FallbackFromClassKey = slot?.FallbackFromRuleId == null ? null : classes.FirstOrDefault(x => x.RuleId == slot.FallbackFromRuleId)?.ClassKey,
                    Count = set?.Items.Count ?? 0,
                    Max = c.Single ? 1 : rule?.Params?.MaxCount,
                    Items = [.. (set?.Items ?? []).Select((i, index) => new MediaItemDto
                    {
                        Id = i.Id,
                        Position = index + 1,
                        FileName = i.FileName,
                        Width = i.Width,
                        Height = i.Height,
                        Checksum = i.Checksum,
                        State = i.State,
                        Url = i.Url,
                    })],
                };
            })],
        };
    }

    /// <summary>
    /// A catalog rule in a list.
    /// </summary>
    /// <param name="rule"></param>
    /// <param name="today"></param>
    /// <param name="language"></param>
    /// <param name="affectedAppCount"></param>
    /// <returns></returns>
    public static RuleListDto Rule(CatalogRule rule, DateOnly today, string language, int affectedAppCount) => Fill(new RuleListDto(), rule, today, language, affectedAppCount);

    /// <summary>
    /// Copies the list fields of <paramref name="rule"/> into <paramref name="dto"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="dto"></param>
    /// <param name="rule"></param>
    /// <param name="today"></param>
    /// <param name="language"></param>
    /// <param name="affectedAppCount"></param>
    /// <returns></returns>
    public static T Fill<T>(T dto, CatalogRule rule, DateOnly today, string language, int affectedAppCount) where T : RuleListDto
    {
        dto.Id = rule.Id;
        dto.Store = rule.StoreKind ?? StoreKind.Apple;
        dto.Category = rule.Category;
        dto.Title = rule.Title?.In(language);
        dto.Level = rule.Requirement?.Level;
        dto.Status = RuleEvaluator.StatusOn(rule, today);
        dto.EffectiveDate = rule.EffectiveDate;
        dto.Deadline = rule.Deadline;
        dto.DueDate = RuleEvaluator.DueDate(rule);
        dto.CheckKind = rule.Check?.Kind;
        dto.Verified = rule.Verified;
        dto.SourceUrl = rule.Sources?.FirstOrDefault()?.Url;
        dto.CheckedDate = rule.Sources?.FirstOrDefault()?.CheckedAt;
        dto.AffectedAppCount = affectedAppCount;

        return dto;
    }

    /// <summary>
    /// An assisted step row in a list.
    /// </summary>
    /// <param name="row"></param>
    /// <param name="definition"></param>
    /// <param name="catalog"></param>
    /// <param name="localizer"></param>
    /// <param name="language"></param>
    /// <returns></returns>
    public static AssistedStepListDto Step(AssistedStep row, AssistedStepDefinition definition, RuleCatalog catalog, IMilvaLocalizer localizer, string language)
        => FillStep(new AssistedStepListDto(), row, definition, catalog, localizer, language);

    /// <summary>
    /// Copies the list fields of an assisted step into <paramref name="dto"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="dto"></param>
    /// <param name="row"></param>
    /// <param name="definition"></param>
    /// <param name="catalog"></param>
    /// <param name="localizer"></param>
    /// <param name="language"></param>
    /// <returns></returns>
    public static T FillStep<T>(T dto, AssistedStep row, AssistedStepDefinition definition, RuleCatalog catalog, IMilvaLocalizer localizer, string language) where T : AssistedStepListDto
    {
        var rule = definition?.RuleId == null ? null : catalog.Find(definition.RuleId);

        dto.Id = row.Id;
        dto.AppId = row.AppId;
        dto.Key = row.Key;
        dto.RuleId = definition?.RuleId;
        dto.Store = row.Store;
        dto.Title = rule?.Title?.In(language) ?? (definition == null ? row.Key : localizer[AssistedStepCatalog.TextKey(definition, "Title")]);
        dto.Status = row.Status;
        dto.DueDate = rule == null ? null : RuleEvaluator.DueDate(rule);
        dto.LastDoneDate = row.CompletedDate;
        dto.LastDoneByUserName = row.CompletedByUserName;
        dto.Channel = row.CompletedChannel;

        return dto;
    }

    /// <summary>
    /// Checklist of a step in the language of the request, with the items marked done.
    /// </summary>
    /// <param name="definition"></param>
    /// <param name="doneItems"></param>
    /// <param name="localizer"></param>
    /// <returns></returns>
    public static List<AssistedStepItemDto> Checklist(AssistedStepDefinition definition, IReadOnlyCollection<int> doneItems, IMilvaLocalizer localizer)
        => [.. Enumerable.Range(1, definition.StepCount).Select(n => new AssistedStepItemDto
        {
            Order = n,
            Text = localizer[AssistedStepCatalog.TextKey(definition, $"Step{n}")],
            Done = doneItems?.Contains(n) == true,
        })];

    /// <summary>
    /// Known traps of a step in the language of the request.
    /// </summary>
    /// <param name="definition"></param>
    /// <param name="localizer"></param>
    /// <returns></returns>
    public static List<string> Pitfalls(AssistedStepDefinition definition, IMilvaLocalizer localizer)
        => [.. Enumerable.Range(1, definition.PitfallCount).Select(n => (string)localizer[AssistedStepCatalog.TextKey(definition, $"Pitfall{n}")])];

    /// <summary>
    /// The saved answer documents of <paramref name="kinds"/> as JSON by kind name (no secrets: they are never in the
    /// documents).
    /// </summary>
    /// <param name="rows"></param>
    /// <param name="kinds"></param>
    /// <returns></returns>
    public static Dictionary<string, JsonElement> Answers(IEnumerable<SavedAnswer> rows, IEnumerable<SavedAnswerKind> kinds)
    {
        var wanted = kinds.ToHashSet();

        return rows.Where(r => wanted.Contains(r.Kind) && !string.IsNullOrWhiteSpace(r.Answers))
                   .ToDictionary(r => r.Kind.ToString(), r => JsonDocument.Parse(r.Answers).RootElement.Clone());
    }
}
