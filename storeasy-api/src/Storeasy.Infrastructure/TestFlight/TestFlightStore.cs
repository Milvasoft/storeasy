using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Interfaces;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.Domain.TestFlight;

namespace Storeasy.Infrastructure.TestFlight;

/// <summary>
/// Saves a TestFlight snapshot of one App Store record: groups, testers and localizations are upserted by the store's id
/// (ids stay stable for the web app) and the missing ones removed; the beta details of the known builds are upserted and
/// the builds' expiry refreshed. All writes are set based or bulk.
/// </summary>
/// <param name="groupRepository"></param>
/// <param name="testerRepository"></param>
/// <param name="localizationRepository"></param>
/// <param name="betaBuildRepository"></param>
/// <param name="buildRepository"></param>
/// <param name="timeProvider"></param>
public sealed class TestFlightStore(IStoreasyRepositoryBase<BetaGroup> groupRepository,
                                    IStoreasyRepositoryBase<BetaTester> testerRepository,
                                    IStoreasyRepositoryBase<BetaAppLocalization> localizationRepository,
                                    IStoreasyRepositoryBase<BetaBuild> betaBuildRepository,
                                    IStoreasyRepositoryBase<Build> buildRepository,
                                    TimeProvider timeProvider)
{
    private readonly IStoreasyRepositoryBase<BetaGroup> _groupRepository = groupRepository;
    private readonly IStoreasyRepositoryBase<BetaTester> _testerRepository = testerRepository;
    private readonly IStoreasyRepositoryBase<BetaAppLocalization> _localizationRepository = localizationRepository;
    private readonly IStoreasyRepositoryBase<BetaBuild> _betaBuildRepository = betaBuildRepository;
    private readonly IStoreasyRepositoryBase<Build> _buildRepository = buildRepository;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <summary>
    /// Saves <paramref name="snapshot"/> and returns a one line summary.
    /// </summary>
    public async Task<string> SaveAsync(int organizationId, int storeAppId, TestFlightSnapshot snapshot, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        await SaveGroupsAsync(organizationId, storeAppId, snapshot, now, cancellationToken);
        await SaveTestersAsync(organizationId, storeAppId, snapshot.Testers, now, cancellationToken);
        await SaveLocalizationsAsync(organizationId, storeAppId, snapshot.Localizations, now, cancellationToken);
        var builds = await SaveBuildsAsync(organizationId, storeAppId, snapshot.Builds, now, cancellationToken);

        return $"{snapshot.Groups.Count} groups, {snapshot.Testers.Count} testers, {builds} builds, {snapshot.Localizations.Count} localizations.";
    }

    /// <summary>
    /// Marks a build as submitted to beta review (the store may report the new state only a moment later).
    /// </summary>
    public async Task MarkSubmittedAsync(int organizationId, int storeAppId, long buildId, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var updated = await _betaBuildRepository.ExecuteUpdateAsync(b => b.BuildId == buildId && (b.BetaReviewState == BetaReviewState.NotSubmitted || b.BetaReviewState == BetaReviewState.Rejected),
                                                                    new SetPropertyBuilder<BetaBuild>().SetPropertyValue(b => b.BetaReviewState, BetaReviewState.WaitingForReview)
                                                                                                       .SetPropertyValue(b => b.BetaReviewSubmittedDate, (DateTime?)now),
                                                                    cancellationToken);

        if (updated == 0)
        {
            var exists = await _betaBuildRepository.GetFirstOrDefaultAsync(b => b.BuildId == buildId, projection: b => (long?)b.Id, cancellationToken: cancellationToken);

            if (!exists.HasValue)
                await _betaBuildRepository.AddAsync(new BetaBuild { OrganizationId = organizationId, StoreAppId = storeAppId, BuildId = buildId, BetaReviewState = BetaReviewState.WaitingForReview, BetaReviewSubmittedDate = now, SyncedDate = now }, cancellationToken);
        }
    }

    private async Task SaveGroupsAsync(int organizationId, int storeAppId, TestFlightSnapshot snapshot, DateTime now, CancellationToken cancellationToken)
    {
        var existing = (await _groupRepository.GetAllAsync(g => g.StoreAppId == storeAppId, projection: g => new BetaGroup { Id = g.Id, ExternalId = g.ExternalId }, cancellationToken: cancellationToken)).ToDictionary(g => g.ExternalId);
        var keep = snapshot.Groups.Select(g => g.ExternalId).ToList();

        var rows = snapshot.Groups.Select(g => new BetaGroup
        {
            Id = existing.GetValueOrDefault(g.ExternalId)?.Id ?? 0,
            OrganizationId = organizationId,
            StoreAppId = storeAppId,
            ExternalId = g.ExternalId,
            Name = g.Name,
            IsInternal = g.IsInternal,
            PublicLinkEnabled = g.PublicLinkEnabled,
            PublicLink = g.PublicLink,
            PublicLinkLimit = g.PublicLinkLimit,
            PublicLinkLimitEnabled = g.PublicLinkLimitEnabled,
            TesterCount = snapshot.Testers.Count(t => t.GroupExternalIds.Contains(g.ExternalId)),
            BuildExternalIds = [.. g.BuildExternalIds],
            SyncedDate = now,
        }).ToList();

        await _groupRepository.ExecuteDeleteAsync(g => g.StoreAppId == storeAppId && !keep.Contains(g.ExternalId), cancellationToken: cancellationToken);

        if (rows.Any(r => r.Id != 0))
            await _groupRepository.UpdateAsync(rows.Where(r => r.Id != 0).ToList(), cancellationToken, g => g.Name, g => g.IsInternal, g => g.PublicLinkEnabled, g => g.PublicLink, g => g.PublicLinkLimit, g => g.PublicLinkLimitEnabled, g => g.TesterCount, g => g.BuildExternalIds, g => g.SyncedDate);

        if (rows.Any(r => r.Id == 0))
            await _groupRepository.AddRangeAsync(rows.Where(r => r.Id == 0).ToList(), cancellationToken);
    }

    private async Task SaveTestersAsync(int organizationId, int storeAppId, IReadOnlyList<BetaTesterRecord> testers, DateTime now, CancellationToken cancellationToken)
    {
        var existing = (await _testerRepository.GetAllAsync(t => t.StoreAppId == storeAppId, projection: t => new BetaTester { Id = t.Id, ExternalId = t.ExternalId }, cancellationToken: cancellationToken)).ToDictionary(t => t.ExternalId);
        var keep = testers.Select(t => t.ExternalId).ToList();

        var rows = testers.DistinctBy(t => t.ExternalId).Select(t => new BetaTester
        {
            Id = existing.GetValueOrDefault(t.ExternalId)?.Id ?? 0,
            OrganizationId = organizationId,
            StoreAppId = storeAppId,
            ExternalId = t.ExternalId,
            FirstName = t.FirstName,
            LastName = t.LastName,
            Email = t.Email,
            InviteType = t.InviteType,
            State = t.State,
            GroupKeys = t.GroupExternalIds.Count == 0 ? null : $",{string.Join(",", t.GroupExternalIds)},",
            SyncedDate = now,
        }).ToList();

        await _testerRepository.ExecuteDeleteAsync(t => t.StoreAppId == storeAppId && !keep.Contains(t.ExternalId), cancellationToken: cancellationToken);

        if (rows.Any(r => r.Id != 0))
            await _testerRepository.UpdateAsync(rows.Where(r => r.Id != 0).ToList(), cancellationToken, t => t.FirstName, t => t.LastName, t => t.Email, t => t.InviteType, t => t.State, t => t.GroupKeys, t => t.SyncedDate);

        if (rows.Any(r => r.Id == 0))
            await _testerRepository.AddRangeAsync(rows.Where(r => r.Id == 0).ToList(), cancellationToken);
    }

    private async Task SaveLocalizationsAsync(int organizationId, int storeAppId, IReadOnlyList<BetaLocalizationRecord> localizations, DateTime now, CancellationToken cancellationToken)
    {
        var existing = (await _localizationRepository.GetAllAsync(l => l.StoreAppId == storeAppId, projection: l => new BetaAppLocalization { Id = l.Id, Locale = l.Locale }, cancellationToken: cancellationToken)).ToDictionary(l => l.Locale);
        var keep = localizations.Select(l => l.Locale).ToList();

        var rows = localizations.DistinctBy(l => l.Locale).Select(l => new BetaAppLocalization
        {
            Id = existing.GetValueOrDefault(l.Locale)?.Id ?? 0,
            OrganizationId = organizationId,
            StoreAppId = storeAppId,
            ExternalId = l.ExternalId,
            Locale = l.Locale,
            Description = l.Description,
            FeedbackEmail = l.FeedbackEmail,
            MarketingUrl = l.MarketingUrl,
            PrivacyPolicyUrl = l.PrivacyPolicyUrl,
            SyncedDate = now,
        }).ToList();

        await _localizationRepository.ExecuteDeleteAsync(l => l.StoreAppId == storeAppId && !keep.Contains(l.Locale), cancellationToken: cancellationToken);

        if (rows.Any(r => r.Id != 0))
            await _localizationRepository.UpdateAsync(rows.Where(r => r.Id != 0).ToList(), cancellationToken, l => l.ExternalId, l => l.Description, l => l.FeedbackEmail, l => l.MarketingUrl, l => l.PrivacyPolicyUrl, l => l.SyncedDate);

        if (rows.Any(r => r.Id == 0))
            await _localizationRepository.AddRangeAsync(rows.Where(r => r.Id == 0).ToList(), cancellationToken);
    }

    private async Task<int> SaveBuildsAsync(int organizationId, int storeAppId, IReadOnlyList<BetaBuildRecord> records, DateTime now, CancellationToken cancellationToken)
    {
        var builds = (await _buildRepository.GetAllAsync(b => b.StoreAppId == storeAppId, projection: b => new Build { Id = b.Id, ExternalId = b.ExternalId, ExpiryDate = b.ExpiryDate, ProcessingState = b.ProcessingState }, cancellationToken: cancellationToken)).ToDictionary(b => b.ExternalId);
        var betaBuilds = (await _betaBuildRepository.GetAllAsync(b => b.StoreAppId == storeAppId, projection: b => new BetaBuild { Id = b.Id, BuildId = b.BuildId }, cancellationToken: cancellationToken)).ToDictionary(b => b.BuildId);

        var known = records.Where(r => builds.ContainsKey(r.BuildExternalId)).DistinctBy(r => r.BuildExternalId).ToList();

        var rows = known.Select(r => new BetaBuild
        {
            Id = betaBuilds.GetValueOrDefault(builds[r.BuildExternalId].Id)?.Id ?? 0,
            OrganizationId = organizationId,
            StoreAppId = storeAppId,
            BuildId = builds[r.BuildExternalId].Id,
            BetaReviewState = r.ReviewState,
            WhatToTest = [.. r.WhatToTest],
            SyncedDate = now,
        }).ToList();

        if (rows.Any(r => r.Id != 0))
            await _betaBuildRepository.UpdateAsync(rows.Where(r => r.Id != 0).ToList(), cancellationToken, b => b.BetaReviewState, b => b.WhatToTest, b => b.SyncedDate);

        if (rows.Any(r => r.Id == 0))
            await _betaBuildRepository.AddRangeAsync(rows.Where(r => r.Id == 0).ToList(), cancellationToken);

        var expiry = known.Select(r => (Build: builds[r.BuildExternalId], Record: r))
                          .Where(x => x.Record.ExpirationDate != x.Build.ExpiryDate || (x.Record.Expired && x.Build.ProcessingState != BuildProcessingState.Expired))
                          .Select(x => new Build { Id = x.Build.Id, ExpiryDate = x.Record.ExpirationDate ?? x.Build.ExpiryDate, ProcessingState = x.Record.Expired ? BuildProcessingState.Expired : x.Build.ProcessingState })
                          .ToList();

        if (expiry.Count > 0)
            await _buildRepository.UpdateAsync(expiry, cancellationToken, b => b.ExpiryDate, b => b.ProcessingState);

        return known.Count;
    }
}
