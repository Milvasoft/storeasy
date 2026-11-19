using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.PreReviewDtos;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Stores;
using System.Linq.Expressions;

namespace Storeasy.Application.Utils.Extensions;

/// <summary>
/// Reads of the AI pre-reviews shared by the start command and the queries: the projection, the DTOs, and how a pre-review
/// whose job ended without it (a crash, a cancelled job) is shown: as failed with <see cref="MessageKey.PreReviewInterrupted"/>.
/// </summary>
public static class PreReviewRepositoryExtensions
{
    /// <summary>
    /// Every column of a pre-review.
    /// </summary>
    public static Expression<Func<PreReview, PreReview>> Projection { get; } = r => new PreReview
    {
        Id = r.Id,
        OrganizationId = r.OrganizationId,
        AppId = r.AppId,
        StoreAppId = r.StoreAppId,
        Store = r.Store,
        VersionString = r.VersionString,
        SnapshotHash = r.SnapshotHash,
        Status = r.Status,
        JobId = r.JobId,
        Model = r.Model,
        Language = r.Language,
        Verdict = r.Verdict,
        ApprovalLikelihood = r.ApprovalLikelihood,
        Summary = r.Summary,
        Findings = r.Findings,
        RuntimeChecks = r.RuntimeChecks,
        PositiveNotes = r.PositiveNotes,
        ImageCount = r.ImageCount,
        InputTokens = r.InputTokens,
        OutputTokens = r.OutputTokens,
        CacheReadInputTokens = r.CacheReadInputTokens,
        CacheCreationInputTokens = r.CacheCreationInputTokens,
        EstimatedCostUsd = r.EstimatedCostUsd,
        ErrorKey = r.ErrorKey,
        CompletedDate = r.CompletedDate,
        CreationDate = r.CreationDate,
        CreatorUserName = r.CreatorUserName,
    };

    /// <summary>
    /// Marks the queued or running pre-reviews whose job has finished (or is gone) as failed with
    /// <see cref="MessageKey.PreReviewInterrupted"/>, in memory, for display. A queued pre-review without a job yet is left
    /// alone.
    /// </summary>
    /// <param name="jobRepository"></param>
    /// <param name="rows"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task ResolveInterruptedAsync(this IStoreasyRepositoryBase<StoreJob> jobRepository, IReadOnlyCollection<PreReview> rows, CancellationToken cancellationToken)
    {
        var open = rows.Where(r => r.Status is PreReviewStatus.Queued or PreReviewStatus.Running && r.JobId != null).ToList();

        if (open.Count == 0)
            return;

        var jobIds = open.Select(r => r.JobId.Value).Distinct().ToList();
        var jobs = await jobRepository.GetAllAsync(j => jobIds.Contains(j.Id), projection: j => new StoreJob { Id = j.Id, Status = j.Status }, cancellationToken: cancellationToken);
        var running = jobs.Where(j => !StoreJobRules.IsFinished(j.Status)).Select(j => j.Id).ToHashSet();

        foreach (var row in open.Where(r => !running.Contains(r.JobId.Value)))
        {
            row.Status = PreReviewStatus.Failed;
            row.ErrorKey = MessageKey.PreReviewInterrupted;
        }
    }

    /// <summary>
    /// The detail DTO; the failure message is localized with <paramref name="localizer"/>.
    /// </summary>
    /// <param name="row"></param>
    /// <param name="localizer"></param>
    /// <returns></returns>
    public static PreReviewDto ToDto(this PreReview row, IMilvaLocalizer localizer) => new()
    {
        Id = row.Id,
        AppId = row.AppId,
        StoreAppId = row.StoreAppId,
        Store = row.Store,
        VersionString = row.VersionString,
        Status = row.Status,
        JobId = row.JobId,
        Model = row.Model,
        Language = row.Language,
        Verdict = row.Verdict,
        ApprovalLikelihood = row.ApprovalLikelihood,
        Summary = row.Summary,
        Findings = [.. (row.Findings ?? []).Select(f => new PreReviewFindingDto
        {
            Severity = f.Severity,
            Area = f.Area,
            Guideline = f.Guideline,
            Title = f.Title,
            Evidence = f.Evidence,
            Fix = f.Fix,
        })],
        RuntimeChecks = row.RuntimeChecks ?? [],
        PositiveNotes = row.PositiveNotes ?? [],
        ImageCount = row.ImageCount,
        InputTokens = row.InputTokens,
        OutputTokens = row.OutputTokens,
        CacheReadInputTokens = row.CacheReadInputTokens,
        CacheCreationInputTokens = row.CacheCreationInputTokens,
        EstimatedCostUsd = row.EstimatedCostUsd,
        ErrorKey = row.ErrorKey,
        ErrorMessage = row.ErrorKey == null ? null : localizer[row.ErrorKey],
        CreatedDate = row.CreationDate ?? default,
        CreatedByUserName = row.CreatorUserName,
        CompletedDate = row.CompletedDate,
    };

    /// <summary>
    /// The history DTO.
    /// </summary>
    /// <param name="row"></param>
    /// <returns></returns>
    public static PreReviewListDto ToListDto(this PreReview row) => new()
    {
        Id = row.Id,
        Store = row.Store,
        VersionString = row.VersionString,
        Status = row.Status,
        Verdict = row.Verdict,
        ApprovalLikelihood = row.ApprovalLikelihood,
        Model = row.Model,
        BlockerCount = row.Findings?.Count(f => f.Severity == PreReviewSeverity.Blocker) ?? 0,
        MajorCount = row.Findings?.Count(f => f.Severity == PreReviewSeverity.Major) ?? 0,
        MinorCount = row.Findings?.Count(f => f.Severity == PreReviewSeverity.Minor) ?? 0,
        EstimatedCostUsd = row.EstimatedCostUsd,
        CreatedDate = row.CreationDate ?? default,
        CreatedByUserName = row.CreatorUserName,
    };
}
