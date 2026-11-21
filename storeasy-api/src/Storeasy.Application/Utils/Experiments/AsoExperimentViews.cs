using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.AsoExperimentDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Content;
using Storeasy.Domain.Experiments;

namespace Storeasy.Application.Utils.Experiments;

/// <summary>
/// Read models of experiment records: the list row, the detail with its timeline, the Play assisted step (Play Console
/// has the experiments, storeasy guides the member there) and the store capabilities shown on the empty states.
/// </summary>
public static class AsoExperimentViews
{
    /// <summary>
    /// Assisted step of a Play store listing experiment (texts in the resx under <c>AssistedStep.google.listing-experiment</c>).
    /// </summary>
    public static AssistedStepDefinition PlayExperimentStep { get; } = new("google.listing-experiment", StoreKind.Google, null, "https://play.google.com/console", [], 6, 2);

    /// <summary>
    /// Assisted step of a Play custom store listing (texts under <c>AssistedStep.google.custom-listing</c>).
    /// </summary>
    public static AssistedStepDefinition PlayCustomListingStep { get; } = new("google.custom-listing", StoreKind.Google, null, "https://play.google.com/console", [], 5, 1);

    /// <summary>
    /// App Store Connect page of an app's distribution (product page optimization and custom product pages live there).
    /// </summary>
    /// <param name="externalId"></param>
    /// <returns></returns>
    public static string AppStoreConnectUrl(string externalId) => $"https://appstoreconnect.apple.com/apps/{Uri.EscapeDataString(externalId ?? string.Empty)}/distribution";

    /// <summary>
    /// What a store record supports.
    /// </summary>
    /// <param name="storeAppId"></param>
    /// <param name="store"></param>
    /// <returns></returns>
    public static AsoStoreCapabilityDto Capability(int storeAppId, StoreKind store) => new()
    {
        StoreAppId = storeAppId,
        Store = store,
        Kinds = [.. AsoExperimentRules.KindsOf(store).Select(k => new AsoKindSupportDto { Kind = k, Support = AsoExperimentRules.Support(store, k) })],
    };

    /// <summary>
    /// The list row of a record.
    /// </summary>
    /// <param name="record"></param>
    /// <returns></returns>
    public static AsoExperimentSummaryDto Summary(AsoExperiment record) => Fill(new AsoExperimentSummaryDto(), record);

    /// <summary>
    /// The detail of a record.
    /// </summary>
    /// <param name="record"></param>
    /// <param name="externalAppId">The store's id of the app (console link).</param>
    /// <param name="plans">The record's plans.</param>
    /// <param name="now"></param>
    /// <param name="localizer"></param>
    /// <returns></returns>
    public static AsoExperimentDto Detail(AsoExperiment record, string externalAppId, IReadOnlyList<ChangePlan> plans, DateTime now, IMilvaLocalizer localizer)
    {
        var dto = Fill(new AsoExperimentDto(), record);

        dto.ExternalId = record.ExternalId;
        dto.Variants = record.Variants ?? [];
        dto.ResultSummary = record.ResultSummary;
        dto.ResultUrl = record.ResultUrl;
        dto.Note = record.Note;
        dto.ReviewRequired = record.ReviewRequired;
        dto.ChecklistDone = record.ChecklistDone ?? [];
        dto.CreatedByUserName = record.CreatorUserName;
        dto.SyncedDate = record.SyncedDate;
        dto.Operations = [.. AsoExperimentRules.Operations(record.Kind, record.Status, record.ExternalId != null, record.ReviewRequired, record.WinnerVariantKey != null)];
        dto.ConsoleUrl = record.Store == StoreKind.Apple ? AppStoreConnectUrl(externalAppId) : StepOf(record.Kind)?.StoreUrl(externalAppId);
        dto.Timeline = Timeline(record, plans, now);

        var step = StepOf(record.Kind);

        if (step != null)
        {
            dto.Assisted = new AsoAssistedDto
            {
                Url = step.StoreUrl(externalAppId),
                Steps = [.. ContentViews.Checklist(step, record.ChecklistDone, localizer).Select(i => i.Text)],
                Pitfalls = ContentViews.Pitfalls(step, localizer),
            };
        }

        return dto;
    }

    /// <summary>
    /// The assisted step of a Play kind, or null.
    /// </summary>
    /// <param name="kind"></param>
    /// <returns></returns>
    public static AssistedStepDefinition StepOf(AsoExperimentKind kind) => kind switch
    {
        AsoExperimentKind.PlayListingExperiment => PlayExperimentStep,
        AsoExperimentKind.PlayCustomListing => PlayCustomListingStep,
        _ => null,
    };

    /// <summary>
    /// What happened to a record, oldest first: its creation, its plans, the submission, start and end, the recorded
    /// result and the last store read.
    /// </summary>
    /// <param name="record"></param>
    /// <param name="plans"></param>
    /// <param name="now"></param>
    /// <returns></returns>
    public static List<AsoTimelineEntryDto> Timeline(AsoExperiment record, IReadOnlyList<ChangePlan> plans, DateTime now)
    {
        var entries = new List<AsoTimelineEntryDto>();

        if (record.CreationDate is { } created)
            entries.Add(new AsoTimelineEntryDto { Date = created, Kind = "created", Title = record.Name });

        foreach (var plan in plans ?? [])
        {
            entries.Add(new AsoTimelineEntryDto
            {
                Date = plan.AppliedDate ?? plan.DecisionDate ?? plan.CreationDate ?? now,
                Kind = "plan",
                Title = plan.Title,
                PlanId = plan.Id,
                PlanStatus = ChangePlanRules.Effective(plan.Status, plan.ExpiryDate, null, now),
            });
        }

        if (record.SubmittedDate is { } submitted)
            entries.Add(new AsoTimelineEntryDto { Date = submitted, Kind = "submitted", Title = record.StoreState ?? record.Status.ToString() });

        if (record.StartDate is { } start)
            entries.Add(new AsoTimelineEntryDto { Date = start, Kind = "started", Title = record.Name });

        if (record.EndDate is { } end)
            entries.Add(new AsoTimelineEntryDto { Date = end, Kind = "ended", Title = record.Status.ToString() });

        if ((record.ResultSummary != null || record.WinnerVariantKey != null) && record.LastModificationDate is { } modified)
            entries.Add(new AsoTimelineEntryDto { Date = modified, Kind = "result", Title = record.WinnerVariantKey ?? record.ResultSummary });

        if (record.SyncedDate is { } synced)
            entries.Add(new AsoTimelineEntryDto { Date = synced, Kind = "synced", Title = record.StoreState ?? record.Status.ToString() });

        return [.. entries.OrderBy(e => e.Date)];
    }

    private static T Fill<T>(T dto, AsoExperiment record) where T : AsoExperimentSummaryDto
    {
        dto.Id = record.Id;
        dto.AppId = record.AppId;
        dto.StoreAppId = record.StoreAppId;
        dto.Store = record.Store;
        dto.Kind = record.Kind;
        dto.Name = record.Name;
        dto.Status = record.Status;
        dto.StoreState = record.StoreState;
        dto.TrafficProportion = record.TrafficProportion;
        dto.VariantCount = record.Variants?.Count ?? 0;
        dto.StartDate = record.StartDate;
        dto.EndDate = record.EndDate;
        dto.WinnerVariantKey = record.WinnerVariantKey;
        dto.Url = record.Url;
        dto.UpdatedDate = record.LastModificationDate ?? record.CreationDate ?? DateTime.MinValue;

        return dto;
    }
}
