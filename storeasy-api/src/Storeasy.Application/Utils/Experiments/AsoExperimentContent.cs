using Storeasy.Domain.Experiments;

namespace Storeasy.Application.Utils.Experiments;

/// <summary>
/// The store state of experiments as content values, so change plans can fingerprint it (an approval after someone
/// changed the experiment in App Store Connect is refused), and the merge of a store read into the records.
/// </summary>
public static class AsoExperimentContent
{
    /// <summary>
    /// Key prefix of experiment values.
    /// </summary>
    public const string Prefix = "aso|";

    /// <summary>
    /// Content key of one value of a store record.
    /// </summary>
    /// <param name="kind"></param>
    /// <param name="externalId"></param>
    /// <param name="field"></param>
    /// <returns></returns>
    public static string KeyOf(AsoExperimentKind kind, string externalId, string field)
        => $"{Prefix}{(kind == AsoExperimentKind.CustomProductPage ? "cpp" : "ppo")}|{externalId}|{field}";

    /// <summary>
    /// Keys a plan of a written record is based on: its state and, for experiments, its treatments.
    /// </summary>
    /// <param name="kind"></param>
    /// <param name="externalId"></param>
    /// <returns></returns>
    public static List<string> BaseKeysOf(AsoExperimentKind kind, string externalId)
        => externalId == null ? [] : [KeyOf(kind, externalId, "state"), KeyOf(kind, externalId, kind == AsoExperimentKind.CustomProductPage ? "version" : "treatments")];

    /// <summary>
    /// Every value of a store read by content key.
    /// </summary>
    /// <param name="read"></param>
    /// <returns></returns>
    public static StoreContentSnapshot AsContent(StoreAsoRead read)
    {
        var snapshot = new StoreContentSnapshot { Store = StoreKind.Apple };

        foreach (var experiment in read?.Experiments ?? [])
        {
            snapshot.Values[KeyOf(AsoExperimentKind.ProductPageOptimization, experiment.Id, "state")] = experiment.State;
            snapshot.Values[KeyOf(AsoExperimentKind.ProductPageOptimization, experiment.Id, "treatments")] = string.Join(",", experiment.Treatments.Select(t => t.Id).Order(StringComparer.Ordinal));
        }

        foreach (var page in read?.CustomPages ?? [])
        {
            snapshot.Values[KeyOf(AsoExperimentKind.CustomProductPage, page.Id, "state")] = page.VersionState;
            snapshot.Values[KeyOf(AsoExperimentKind.CustomProductPage, page.Id, "version")] = page.VersionId;
        }

        return snapshot;
    }

    /// <summary>
    /// Copies what the store says about <paramref name="record"/> into it (state, dates, address, ids). Returns whether
    /// the record is still in the store; a record that is not is left as it is.
    /// </summary>
    /// <param name="record"></param>
    /// <param name="read"></param>
    /// <returns></returns>
    public static bool Merge(AsoExperiment record, StoreAsoRead read)
    {
        if (record.ExternalId == null)
            return false;

        if (record.Kind == AsoExperimentKind.ProductPageOptimization)
        {
            var experiment = read.Experiments.FirstOrDefault(e => e.Id == record.ExternalId);

            if (experiment == null)
                return false;

            record.StoreState = Trim(experiment.State, 50);
            record.TrafficProportion = experiment.TrafficProportion ?? record.TrafficProportion;
            record.ReviewRequired = experiment.ReviewRequired;
            record.StartDate = experiment.StartDate ?? record.StartDate;
            record.EndDate = experiment.EndDate ?? record.EndDate;
            record.Status = AsoExperimentRules.StatusOfExperiment(experiment.State, experiment.StartDate, experiment.EndDate);

            foreach (var treatment in experiment.Treatments)
            {
                var variant = record.Variants.FirstOrDefault(v => v.ExternalId == treatment.Id)
                              ?? record.Variants.FirstOrDefault(v => v.ExternalId == null && string.Equals(v.Name, treatment.Name, StringComparison.Ordinal));

                if (variant != null)
                    variant.ExternalId = treatment.Id;
            }

            return true;
        }

        var page = read.CustomPages.FirstOrDefault(p => p.Id == record.ExternalId);

        if (page == null)
            return false;

        record.StoreState = Trim(page.VersionState, 50);
        record.ExternalVersionId = page.VersionId ?? record.ExternalVersionId;
        record.Url = Trim(page.Url, AsoExperiment.UrlMaxLength) ?? record.Url;
        record.Status = AsoExperimentRules.StatusOfCustomPage(page.VersionState, page.Visible);

        return true;
    }

    /// <summary>
    /// Records for experiments and pages made outside storeasy (in App Store Connect), so the timeline knows them too.
    /// Their variants carry names only.
    /// </summary>
    /// <param name="app"></param>
    /// <param name="read"></param>
    /// <param name="known">External ids storeasy already has.</param>
    /// <returns></returns>
    public static List<AsoExperiment> Discover(StoreAppContext app, StoreAsoRead read, IReadOnlySet<string> known)
    {
        var found = new List<AsoExperiment>();

        foreach (var experiment in read.Experiments.Where(e => !known.Contains(e.Id)))
        {
            var record = New(app, AsoExperimentKind.ProductPageOptimization, experiment.Name, experiment.Id);

            record.Variants = [.. experiment.Treatments.Select((t, i) => new AsoExperimentVariant { Key = VariantKey(i), Name = t.Name, AppIconName = t.AppIconName, ExternalId = t.Id })];
            Merge(record, read);
            found.Add(record);
        }

        foreach (var page in read.CustomPages.Where(p => !known.Contains(p.Id)))
        {
            var record = New(app, AsoExperimentKind.CustomProductPage, page.Name, page.Id);

            record.Variants = [new AsoExperimentVariant
            {
                Key = VariantKey(0),
                Name = page.Name,
                Locales = [.. page.Localizations.Select(l => new AsoVariantLocale { Locale = l.Locale, PromotionalText = l.PromotionalText })],
            }];
            Merge(record, read);
            found.Add(record);
        }

        return found;
    }

    /// <summary>
    /// Key of the variant at <paramref name="index"/> ("A", "B", ...).
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public static string VariantKey(int index) => ((char)('A' + index)).ToString();

    private static AsoExperiment New(StoreAppContext app, AsoExperimentKind kind, string name, string externalId) => new()
    {
        OrganizationId = app.OrganizationId,
        AppId = app.AppId,
        StoreAppId = app.StoreAppId,
        Store = app.Store,
        Kind = kind,
        Name = Trim(string.IsNullOrWhiteSpace(name) ? externalId : name, AsoExperiment.NameMaxLength),
        ExternalId = externalId,
        Status = AsoExperimentStatus.Prepared,
    };

    private static string Trim(string value, int max) => value == null || value.Length <= max ? value : value[..max];
}
