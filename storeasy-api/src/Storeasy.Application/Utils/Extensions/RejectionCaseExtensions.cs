using Storeasy.Domain.Feedback;

namespace Storeasy.Application.Utils.Extensions;

/// <summary>
/// Reads of rejection cases shared by the rejection features.
/// </summary>
public static class RejectionCaseExtensions
{
    /// <summary>
    /// Reads one case of the organization with every column the features change (null when it is not there).
    /// </summary>
    /// <param name="caseRepository"></param>
    /// <param name="organizationId"></param>
    /// <param name="caseId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static Task<RejectionCase> GetCaseAsync(this IStoreasyRepositoryBase<RejectionCase> caseRepository, int organizationId, int caseId, CancellationToken cancellationToken)
        => caseRepository.GetFirstOrDefaultAsync(c => c.Id == caseId && c.OrganizationId == organizationId,
                                                 projection: c => new RejectionCase
                                                 {
                                                     Id = c.Id,
                                                     OrganizationId = c.OrganizationId,
                                                     AppId = c.AppId,
                                                     StoreAppId = c.StoreAppId,
                                                     Store = c.Store,
                                                     ExternalVersionId = c.ExternalVersionId,
                                                     VersionString = c.VersionString,
                                                     BuildNumber = c.BuildNumber,
                                                     Kind = c.Kind,
                                                     State = c.State,
                                                     OpenedVia = c.OpenedVia,
                                                     OpenedDate = c.OpenedDate,
                                                     ClosedDate = c.ClosedDate,
                                                     ClosedVia = c.ClosedVia,
                                                     CloseNote = c.CloseNote,
                                                     RawText = c.RawText,
                                                     RawTextByUserName = c.RawTextByUserName,
                                                     RawTextDate = c.RawTextDate,
                                                     Guidelines = c.Guidelines,
                                                     Checklist = c.Checklist,
                                                     ReplyDraft = c.ReplyDraft,
                                                     ReplyDraftByUserName = c.ReplyDraftByUserName,
                                                     ReplyDraftChannel = c.ReplyDraftChannel,
                                                     ReplyDraftDate = c.ReplyDraftDate,
                                                     ReplySentDate = c.ReplySentDate,
                                                     ReplySentByUserName = c.ReplySentByUserName,
                                                     PreviousCaseId = c.PreviousCaseId,
                                                     Timeline = c.Timeline,
                                                 },
                                                 cancellationToken: cancellationToken);
}
