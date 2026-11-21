using Storeasy.Domain.Rules;

namespace Storeasy.Application.Utils.Content;

/// <summary>
/// Definition of an assisted step (docs/urun-plani.md 11.4).
/// </summary>
/// <param name="Key">Step key: a built in key or the catalog rule id.</param>
/// <param name="Store">Store.</param>
/// <param name="RuleId">Catalog rule, when the step comes from one.</param>
/// <param name="StoreUrlTemplate">Store page; <c>{externalId}</c> is the store's id of the app.</param>
/// <param name="AnswerKeys">Saved answer documents the step uses.</param>
/// <param name="StepCount">Checklist items (resx <c>AssistedStep.{Key}.Step{n}</c>, generic ones for catalog rules).</param>
/// <param name="PitfallCount">Known traps (resx <c>AssistedStep.{Key}.Pitfall{n}</c>).</param>
public sealed record AssistedStepDefinition(string Key, StoreKind Store, string RuleId, string StoreUrlTemplate, IReadOnlyList<SavedAnswerKind> AnswerKeys, int StepCount, int PitfallCount)
{
    /// <summary>
    /// Store page of the step for an app.
    /// </summary>
    /// <param name="externalId"></param>
    /// <returns></returns>
    public string StoreUrl(string externalId) => this.StoreUrlTemplate.Replace("{externalId}", Uri.EscapeDataString(externalId ?? string.Empty), StringComparison.Ordinal);
}

/// <summary>
/// The assisted steps storeasy knows: built in steps for store forms without an API, and every catalog rule checked as
/// <c>assisted</c> (except the rejection reply, which belongs to the rejection flow). Texts live in the resx files.
/// </summary>
public static class AssistedStepCatalog
{
    /// <summary>
    /// Resx key prefix of step texts.
    /// </summary>
    public const string ResourcePrefix = "AssistedStep";

    /// <summary>
    /// Key of the generic checklist used by catalog rules.
    /// </summary>
    public const string GenericKey = "Generic";

    /// <summary>
    /// Items of the generic checklist.
    /// </summary>
    public const int GenericStepCount = 3;

    private const string _appStoreConnect = "https://appstoreconnect.apple.com/apps/{externalId}/distribution";
    private const string _playConsole = "https://play.google.com/console";
    private const string _partnerCenter = "https://partner.microsoft.com/dashboard/products/{externalId}/overview";

    /// <summary>
    /// Built in steps.
    /// </summary>
    public static IReadOnlyList<AssistedStepDefinition> BuiltIn { get; } =
    [
        new("apple.privacy.app-privacy", StoreKind.Apple, null, _appStoreConnect + "/privacy", [SavedAnswerKind.PrivacyLabels], 4, 1),
        new("google.content-rating", StoreKind.Google, null, _playConsole, [SavedAnswerKind.ContentRating], 3, 1),
        new("google.target-audience", StoreKind.Google, null, _playConsole, [SavedAnswerKind.TargetAudience], 3, 0),
        new("google.app-access", StoreKind.Google, null, _playConsole, [SavedAnswerKind.ReviewLogin, SavedAnswerKind.ReviewNotes], 3, 1),
        new("google.category", StoreKind.Google, null, _playConsole, [], 2, 0),
        new(MsStore.MsAssistedSteps.FlightGroups, StoreKind.Microsoft, null, _partnerCenter, [], 2, 0),
        new(MsStore.MsAssistedSteps.Reviews, StoreKind.Microsoft, null, _partnerCenter, [], 2, 0),
    ];

    /// <summary>
    /// Steps recorded only when they come up: Microsoft Store items the API refuses or that belong to Partner Center
    /// (persisted by the Microsoft features, not listed for every app).
    /// </summary>
    public static IReadOnlyList<AssistedStepDefinition> Conditional { get; } =
    [
        new(MsStore.MsAssistedSteps.ReserveName, StoreKind.Microsoft, null, "https://partner.microsoft.com/dashboard/apps-and-games/overview", [], 2, 0),
        new(MsStore.MsAssistedSteps.Pricing, StoreKind.Microsoft, null, _partnerCenter, [], 2, 0),
        new(MsStore.MsAssistedSteps.AddonUpdate, StoreKind.Microsoft, null, _partnerCenter, [], 2, 0),
        new(MsStore.MsAssistedSteps.MandatoryUpdate, StoreKind.Microsoft, null, _partnerCenter, [], 2, 0),
        new(MsStore.MsAssistedSteps.ManualEdit, StoreKind.Microsoft, null, _partnerCenter, [], 2, 0),
        new(MsStore.MsAssistedSteps.CertificationReport, StoreKind.Microsoft, null, _partnerCenter, [], 2, 0),
    ];

    /// <summary>
    /// Every step that applies (or may apply) to an app in a store: the built in steps of the store and the assisted catalog
    /// rules whose conditions do not rule the app out.
    /// </summary>
    /// <param name="catalog"></param>
    /// <param name="subject"></param>
    /// <returns></returns>
    public static List<AssistedStepDefinition> For(RuleCatalog catalog, RuleSubject subject)
    {
        var steps = BuiltIn.Where(s => s.Store == subject.Store).ToList();

        steps.AddRange(catalog.ForStore(subject.Store)
                              .Where(r => r.Check?.Kind == RuleCheck.Assisted && r.Category != "review")
                              .Where(r => RuleEvaluator.Evaluate(r, subject).Applicability != RuleApplicability.DoesNotApply)
                              .Select(r => new AssistedStepDefinition(r.Id,
                                                                      subject.Store,
                                                                      r.Id,
                                                                      StoreUrlOf(subject.Store),
                                                                      AnswerKeysOf(r),
                                                                      GenericStepCount,
                                                                      0)));

        return steps;
    }

    /// <summary>
    /// The definition of <paramref name="key"/> in <paramref name="store"/>, or null.
    /// </summary>
    /// <param name="catalog"></param>
    /// <param name="store"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    public static AssistedStepDefinition Find(RuleCatalog catalog, StoreKind store, string key)
    {
        var builtIn = BuiltIn.Concat(Conditional).Concat(Monetization.MonetizationAssistedSteps.All).FirstOrDefault(s => s.Store == store && s.Key == key);

        if (builtIn != null)
            return builtIn;

        var rule = catalog.Find(key);

        return rule == null || rule.StoreKind != store
            ? null
            : new AssistedStepDefinition(rule.Id, store, rule.Id, StoreUrlOf(store), AnswerKeysOf(rule), GenericStepCount, 0);
    }

    /// <summary>
    /// Resx key of a text of a step (<c>Title</c>, <c>Step1</c>, <c>Pitfall1</c>...).
    /// </summary>
    /// <param name="definition"></param>
    /// <param name="part"></param>
    /// <returns></returns>
    public static string TextKey(AssistedStepDefinition definition, string part)
        => $"{ResourcePrefix}.{(definition.RuleId != null && part.StartsWith("Step", StringComparison.Ordinal) ? GenericKey : definition.Key)}.{part}";

    private static List<SavedAnswerKind> AnswerKeysOf(CatalogRule rule)
    {
        var inputs = rule.Check?.Inputs ?? [];

        return inputs.Any(i => i.Contains("dataSafety", StringComparison.OrdinalIgnoreCase)) ? [SavedAnswerKind.DataSafety] : [];
    }

    private static string StoreUrlOf(StoreKind store) => store switch
    {
        StoreKind.Apple => _appStoreConnect + "/info",
        StoreKind.Microsoft => _partnerCenter,
        StoreKind.Google => _playConsole,
        _ => Domain.Stores.StoreChannels.Of(store)?.ConsoleUrl,
    };
}
