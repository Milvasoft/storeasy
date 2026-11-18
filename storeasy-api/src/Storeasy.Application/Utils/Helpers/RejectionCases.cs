using Storeasy.Application.Dtos.FeedbackDtos;
using Storeasy.Domain.Feedback;

namespace Storeasy.Application.Utils.Helpers;

/// <summary>
/// Shared rules of the rejection flow: the reply length as the Resolution Center counts it, the timeline, the App Store
/// Connect page of a rejection and the case read model.
/// </summary>
public static class RejectionCases
{
    /// <summary>
    /// Longest timeline note kept.
    /// </summary>
    public const int TimelineTextMaxLength = 300;

    /// <summary>
    /// Reply length as the Resolution Center counts it: every character, a line break counting once.
    /// </summary>
    public static int ReplyLength(string text) => string.IsNullOrEmpty(text) ? 0 : text.Replace("\r\n", "\n").Length;

    /// <summary>
    /// Appends a timeline line.
    /// </summary>
    public static void AddTimeline(RejectionCase rejectionCase, DateTime date, string kind, string text, ActionChannel channel)
    {
        rejectionCase.Timeline ??= [];
        rejectionCase.Timeline.Add(new RejectionTimelineEntry
        {
            Date = date,
            Kind = kind,
            Text = text?.Length > TimelineTextMaxLength ? text[..TimelineTextMaxLength] : text,
            Channel = channel,
        });
    }

    /// <summary>
    /// App Store Connect page that lists the app's review submissions and their App Review messages (no API reads them).
    /// </summary>
    public static string ResolutionCenterUrl(StoreKind store, string appExternalId) => store switch
    {
        StoreKind.Apple when !string.IsNullOrEmpty(appExternalId) => $"https://appstoreconnect.apple.com/apps/{Uri.EscapeDataString(appExternalId)}/distribution/reviewsubmissions",
        StoreKind.Microsoft => "https://partner.microsoft.com/dashboard/apps-and-games/overview",
        _ => null,
    };

    /// <summary>
    /// A new open case.
    /// </summary>
    public static RejectionCase Open(int organizationId, int appId, int storeAppId, StoreKind store, string externalVersionId, string versionString, string buildNumber, RejectionKind kind, RejectionSource source, DateTime now, ActionChannel channel, string actor)
    {
        var rejectionCase = new RejectionCase
        {
            OrganizationId = organizationId,
            AppId = appId,
            StoreAppId = storeAppId,
            Store = store,
            ExternalVersionId = externalVersionId,
            VersionString = versionString,
            BuildNumber = buildNumber,
            Kind = kind,
            State = RejectionState.Open,
            OpenedVia = source,
            OpenedDate = now,
            Guidelines = [],
            Checklist = [],
            Timeline = [],
        };

        AddTimeline(rejectionCase, now, "opened", $"{kind} ({source}) {actor}".Trim(), channel);

        return rejectionCase;
    }

    /// <summary>
    /// The read model of a case.
    /// </summary>
    public static RejectionCaseDto ToDto(RejectionCase rejectionCase, string appName, string appExternalId, IReadOnlyCollection<string> earlierGuidelines)
    {
        var length = ReplyLength(rejectionCase.ReplyDraft);

        return new RejectionCaseDto
        {
            Id = rejectionCase.Id,
            AppId = rejectionCase.AppId,
            AppName = appName,
            StoreAppId = rejectionCase.StoreAppId,
            Store = rejectionCase.Store,
            ExternalVersionId = rejectionCase.ExternalVersionId,
            VersionString = rejectionCase.VersionString,
            BuildNumber = rejectionCase.BuildNumber,
            Kind = rejectionCase.Kind,
            State = rejectionCase.State,
            OpenedVia = rejectionCase.OpenedVia,
            OpenedDate = rejectionCase.OpenedDate,
            ClosedDate = rejectionCase.ClosedDate,
            ClosedVia = rejectionCase.ClosedVia,
            CloseNote = rejectionCase.CloseNote,
            ResolutionCenterUrl = ResolutionCenterUrl(rejectionCase.Store, appExternalId),
            RawText = rejectionCase.RawText,
            RawTextByUserName = rejectionCase.RawTextByUserName,
            RawTextDate = rejectionCase.RawTextDate,
            Guidelines = rejectionCase.Guidelines ?? [],
            RepeatedGuidelines = [.. (rejectionCase.Guidelines ?? []).Select(g => g.Code).Where(code => earlierGuidelines?.Contains(code) == true)],
            Checklist = rejectionCase.Checklist ?? [],
            PreviousCaseId = rejectionCase.PreviousCaseId,
            Timeline = rejectionCase.Timeline ?? [],
            ReplyDraft = new RejectionReplyDraftDto
            {
                Body = rejectionCase.ReplyDraft,
                Length = length,
                MaxLength = RejectionCase.ReplyMaxLength,
                OverLimit = length > RejectionCase.ReplyMaxLength,
                UpdatedDate = rejectionCase.ReplyDraftDate,
                ByUserName = rejectionCase.ReplyDraftByUserName,
                Channel = rejectionCase.ReplyDraftChannel,
                SentDate = rejectionCase.ReplySentDate,
                SentByUserName = rejectionCase.ReplySentByUserName,
            },
        };
    }
}
