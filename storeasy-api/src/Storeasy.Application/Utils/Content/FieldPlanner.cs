using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Domain.Content;
using Storeasy.Domain.Rules;

namespace Storeasy.Application.Utils.Content;

/// <summary>
/// The planners of the content model: they turn desired values into a <see cref="PlanDraft"/> against the store's current
/// snapshot. Pure functions; the handlers read the store, the change plan service saves the draft.
/// </summary>
public static class FieldPlanner
{
    /// <summary>
    /// A draft that writes the values of <paramref name="desired"/> (content key to value) that differ from the snapshot.
    /// </summary>
    /// <param name="kind"></param>
    /// <param name="title"></param>
    /// <param name="snapshot"></param>
    /// <param name="desired"></param>
    /// <param name="call">Describes the store call of one group of changes.</param>
    /// <returns></returns>
    public static PlanDraft Build(ChangePlanKind kind, string title, StoreContentSnapshot snapshot, IReadOnlyDictionary<string, string> desired, Func<IGrouping<(string Group, string Locale), PlanItem>, PlanCall> call)
    {
        var items = ContentDiff.Compute(snapshot.Values, desired);

        return new PlanDraft
        {
            Kind = kind,
            Title = title,
            VersionId = snapshot.VersionId,
            VersionString = snapshot.VersionString,
            Items = items,
            BaseKeys = [.. desired.Keys],
            Changes = new ContentChangeSet
            {
                VersionId = snapshot.VersionId,
                Fields = [.. items.Select(i => new FieldChange(i.Group, i.Locale, i.Field, desired[ContentKey.Of(i.Group, i.Locale, i.Field)]))],
            },
            Calls = [.. items.GroupBy(i => (i.Group, i.Locale)).Select(call)],
        };
    }

    /// <summary>
    /// The plan for listing text changes. Version fields (except the promotional text, which Apple accepts on any version)
    /// need an editable version; a locale the store does not have yet is created, with a remark.
    /// </summary>
    /// <param name="store"></param>
    /// <param name="snapshot"></param>
    /// <param name="changes">(locale, field, value) triples; fields must be known to <see cref="ListingFields"/>.</param>
    /// <param name="localizer"></param>
    /// <param name="errorKey">Set when the plan cannot be made.</param>
    /// <returns></returns>
    public static PlanDraft Listing(StoreKind store, StoreContentSnapshot snapshot, IEnumerable<(string Locale, string Field, string Value)> changes, IMilvaLocalizer localizer, out string errorKey)
    {
        errorKey = null;

        var desired = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (locale, fieldName, value) in changes)
        {
            var field = ListingFields.Find(store, fieldName);

            if (field == null)
            {
                errorKey = MessageKey.FieldUnknown;
                return null;
            }

            if (field.Group == ContentGroups.Version && field.Field != "promotionalText" && !snapshot.VersionEditable)
            {
                errorKey = MessageKey.VersionNotEditable;
                return null;
            }

            if (field.Group == ContentGroups.Version && snapshot.VersionId == null && store == StoreKind.Apple)
            {
                errorKey = MessageKey.VersionNotFound;
                return null;
            }

            desired[ContentKey.Of(field.Group, field.Localized ? locale : null, field.Field)] = value;
        }

        var details = desired.Keys.All(k => ContentKey.Parse(k).Group == ContentGroups.Details);

        var draft = Build(details ? ChangePlanKind.Details : ChangePlanKind.Listing,
                          $"{(details ? "App details" : "Listing")}: {desired.Count} fields",
                          snapshot,
                          desired,
                          g => ListingCall(store, g.Key.Group, g.Key.Locale, snapshot));

        foreach (var locale in draft.Items.Select(i => i.Locale).Where(l => l != null && !snapshot.Locales.Contains(l, StringComparer.OrdinalIgnoreCase)).Distinct())
            draft.Warnings.Add(new PlanWarning { Key = "Plan.LocaleCreated", Message = localizer[MessageKey.PlanLocaleCreated, locale], Locale = locale });

        var name = snapshot.Get(ContentKey.Of(ContentGroups.AppInfo, snapshot.PrimaryLocale, "name"));

        foreach (var item in draft.Items.Where(i => i.Field == "keywords" && i.After != null))
        {
            var appName = snapshot.Get(ContentKey.Of(ContentGroups.AppInfo, item.Locale, "name")) ?? name;
            var nameWords = (appName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => w.ToLowerInvariant()).ToHashSet();
            var repeated = item.After.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(k => nameWords.Contains(k.ToLowerInvariant())).ToList();

            if (repeated.Count > 0)
                draft.Warnings.Add(new PlanWarning { Key = "Plan.KeywordInName", Message = localizer[MessageKey.PlanKeywordInName, string.Join(", ", repeated)], Field = item.Field, Locale = item.Locale });
        }

        return draft;
    }

    /// <summary>
    /// Characters or bytes over the catalog limit of a listing field: the message key and its arguments, or null.
    /// </summary>
    /// <param name="field"></param>
    /// <param name="value"></param>
    /// <param name="catalog"></param>
    /// <returns></returns>
    public static (string Key, int Actual, int Limit)? LimitViolation(ListingField field, string value, RuleCatalog catalog)
    {
        var rule = catalog.Find(field.LimitRuleId);

        if (rule?.Params == null || string.IsNullOrEmpty(value))
            return null;

        if (rule.Params.MaxBytes is { } maxBytes && ContentDiff.ByteCount(value) > maxBytes)
            return (MessageKey.FieldTooManyBytes, ContentDiff.ByteCount(value), maxBytes);

        if (rule.Params.MaxChars is { } maxChars && ContentDiff.CharCount(value) > maxChars)
            return (MessageKey.FieldTooLong, ContentDiff.CharCount(value), maxChars);

        return null;
    }

    private static PlanCall ListingCall(StoreKind store, string group, string locale, StoreContentSnapshot snapshot)
    {
        var exists = locale == null || snapshot.Locales.Contains(locale, StringComparer.OrdinalIgnoreCase);

        return (store, group) switch
        {
            (StoreKind.Apple, ContentGroups.AppInfo) => new PlanCall { Method = exists ? "PATCH" : "POST", Path = "/v1/appInfoLocalizations", Target = locale },
            (StoreKind.Apple, ContentGroups.Version) => new PlanCall { Method = exists ? "PATCH" : "POST", Path = "/v1/appStoreVersionLocalizations", Target = locale },
            (StoreKind.Apple, _) => new PlanCall { Method = "PATCH", Path = "/v1/endUserLicenseAgreements", Target = "eula" },
            (_, ContentGroups.Details) => new PlanCall { Method = "PATCH", Path = "edits.details", Target = "details" },
            _ => new PlanCall { Method = exists ? "PATCH" : "PUT", Path = "edits.listings", Target = locale },
        };
    }
}
