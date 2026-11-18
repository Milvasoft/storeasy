using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.ReadinessDtos;
using Storeasy.Domain.Rules;

namespace Storeasy.Application.Utils.Content;

/// <summary>
/// The per app compliance timeline (docs/urun-plani.md 12.4): every catalog rule with a date (or a target API or SDK
/// minimum) that applies, or may apply, to the app, with its state. Assisted rules are done when their step is recorded;
/// target API and SDK rules are decided from the app profile; the rest is unknown until storeasy can check it.
/// </summary>
public static class ComplianceTimeline
{
    private static readonly HashSet<string> _alwaysListed = ["target-api", "sdk", "age-rating", "compliance", "policy", "privacy"];

    /// <summary>
    /// Timeline items of one store record.
    /// </summary>
    /// <param name="app"></param>
    /// <param name="subject"></param>
    /// <param name="catalog"></param>
    /// <param name="steps">Assisted step state by key, with the step id.</param>
    /// <param name="today"></param>
    /// <param name="language"></param>
    /// <returns></returns>
    public static List<ComplianceItemDto> Evaluate(StoreAppContext app,
                                                   RuleSubject subject,
                                                   RuleCatalog catalog,
                                                   IReadOnlyDictionary<string, (int Id, AssistedStepStatus Status)> steps,
                                                   DateOnly today,
                                                   string language)
    {
        var items = new List<ComplianceItemDto>();

        foreach (var rule in catalog.ForStore(app.Store))
        {
            var due = RuleEvaluator.DueDate(rule);

            if (due == null && !_alwaysListed.Contains(rule.Category))
                continue;

            if (rule.Category is "review" or "testflight" or "listing-limits")
                continue;

            var match = RuleEvaluator.Evaluate(rule, subject);

            if (match.Applicability == RuleApplicability.DoesNotApply)
                continue;

            var hasStep = steps.TryGetValue(rule.Id, out var step);

            items.Add(new ComplianceItemDto
            {
                AppId = app.AppId,
                AppName = app.AppName,
                RuleId = rule.Id,
                Store = app.Store,
                Title = rule.Title?.In(language),
                Summary = rule.Description?.In(language),
                DueDate = due,
                DaysLeft = due == null ? null : due.Value.DayNumber - today.DayNumber,
                Status = StatusOf(rule, match, hasStep ? step.Status : null, app.Profile),
                RuleStatus = RuleEvaluator.StatusOn(rule, today),
                Applicability = match.Applicability,
                MatchReason = [.. match.Reasons],
                SourceUrl = rule.Sources?.FirstOrDefault()?.Url,
                CheckedDate = rule.Sources?.FirstOrDefault()?.CheckedAt,
                Conflict = rule.Conflicts?.Count > 0 ? string.Join(" ", rule.Conflicts.Select(c => $"{c.Description} {c.CatalogChoice}")) : null,
                AssistedStepId = hasStep ? step.Id : null,
            });
        }

        return [.. items.OrderBy(i => i.DueDate ?? DateOnly.MaxValue).ThenBy(i => i.RuleId, StringComparer.Ordinal)];
    }

    /// <summary>
    /// The nearest open deadline (missing or unknown, due today or later).
    /// </summary>
    /// <param name="items"></param>
    /// <param name="today"></param>
    /// <returns></returns>
    public static DeadlineDto Next(IEnumerable<ComplianceItemDto> items, DateOnly today)
        => items.Where(i => i.DueDate >= today && i.Status is ComplianceStatus.Missing or ComplianceStatus.Unknown)
                .OrderBy(i => i.DueDate)
                .Select(i => new DeadlineDto { Date = i.DueDate.Value, RuleId = i.RuleId, Title = i.Title })
                .FirstOrDefault();

    /// <summary>
    /// State of one applicable rule.
    /// </summary>
    /// <param name="rule"></param>
    /// <param name="match"></param>
    /// <param name="step"></param>
    /// <param name="profile"></param>
    /// <returns></returns>
    public static ComplianceStatus StatusOf(CatalogRule rule, RuleMatch match, AssistedStepStatus? step, AppProfile profile)
    {
        if (rule.Check?.Kind == RuleCheck.Assisted)
        {
            if (step is AssistedStepStatus.Done or AssistedStepStatus.Skipped)
                return ComplianceStatus.Done;

            return match.Applicability == RuleApplicability.Applies ? ComplianceStatus.Missing : ComplianceStatus.Unknown;
        }

        if (rule.Category == "target-api" && rule.Params?.MinTargetSdk is { } minTargetSdk)
            return profile?.TargetSdk is { } targetSdk ? targetSdk >= minTargetSdk ? ComplianceStatus.Done : ComplianceStatus.Missing : ComplianceStatus.Unknown;

        if (rule.Category == "sdk" && rule.Params?.MinSdkVersion is { } minSdk && profile?.BuildSdkVersion != null)
            return RuleEvaluator.VersionAtLeast(profile.BuildSdkVersion, minSdk) ? ComplianceStatus.Done : ComplianceStatus.Missing;

        return ComplianceStatus.Unknown;
    }
}
