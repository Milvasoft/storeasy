using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.NotificationDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Rules;
using System.Globalization;

namespace Storeasy.Infrastructure.Rules;

/// <summary>
/// Default <see cref="IRuleWatcher"/>. The first read of a source only records its baseline; a later read with another
/// hash opens one proposal per new content (a hash already proposed, approved or rejected never opens another) and tells
/// the platform admins.
/// </summary>
/// <param name="sourceRepository"></param>
/// <param name="proposalRepository"></param>
/// <param name="fetcher"></param>
/// <param name="catalogSource"></param>
/// <param name="notificationDispatcher"></param>
/// <param name="options"></param>
/// <param name="timeProvider"></param>
/// <param name="logger"></param>
public sealed class RuleWatcher(IStoreasyRepositoryBase<RuleSource> sourceRepository,
                                IStoreasyRepositoryBase<RuleProposal> proposalRepository,
                                IRuleSourceFetcher fetcher,
                                IRulesCatalogSource catalogSource,
                                INotificationDispatcher notificationDispatcher,
                                RuleWatcherOptions options,
                                TimeProvider timeProvider,
                                IMilvaLogger logger) : IRuleWatcher
{
    private const int _diffLines = 200;

    private readonly IStoreasyRepositoryBase<RuleSource> _sourceRepository = sourceRepository;
    private readonly IStoreasyRepositoryBase<RuleProposal> _proposalRepository = proposalRepository;
    private readonly IRuleSourceFetcher _fetcher = fetcher;
    private readonly IRulesCatalogSource _catalogSource = catalogSource;
    private readonly INotificationDispatcher _notificationDispatcher = notificationDispatcher;
    private readonly RuleWatcherOptions _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly IMilvaLogger _logger = logger;

    /// <inheritdoc/>
    public async Task EnsureSourcesAsync(CancellationToken cancellationToken)
    {
        var configured = (_options.Sources ?? []).Where(s => _fetcher.IsAllowed(s.Url)).DistinctBy(s => s.Url).ToList();

        if (configured.Count == 0)
            return;

        var urls = configured.Select(s => s.Url).ToList();
        var known = (await _sourceRepository.GetAllAsync(s => urls.Contains(s.Url), projection: s => new RuleSource { Url = s.Url }, cancellationToken: cancellationToken)).Select(s => s.Url).ToHashSet();
        var missing = configured.Where(s => !known.Contains(s.Url)).Select(s => new RuleSource { Name = s.Name, Store = s.Store, Url = s.Url, IsActive = true, Status = RuleSourceStatus.New }).ToList();

        if (missing.Count == 0)
            return;

        try
        {
            await _sourceRepository.AddRangeAsync(missing, cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another instance added them at the same time.
        }
    }

    /// <inheritdoc/>
    public async Task<RuleWatchResult> RunAsync(CancellationToken cancellationToken)
    {
        await EnsureSourcesAsync(cancellationToken);

        var sources = await _sourceRepository.GetAllAsync(s => s.IsActive,
                                                          projection: s => new RuleSource { Id = s.Id, Name = s.Name, Store = s.Store, Url = s.Url, ContentHash = s.ContentHash, PreviousContentHash = s.PreviousContentHash, Content = s.Content, Status = s.Status, LastChangeDate = s.LastChangeDate },
                                                          cancellationToken: cancellationToken);

        var sourceIds = sources.Select(s => s.Id).ToList();
        var proposed = (await _proposalRepository.GetAllAsync(p => p.RuleSourceId != null && sourceIds.Contains(p.RuleSourceId.Value),
                                                              projection: p => new RuleProposal { RuleSourceId = p.RuleSourceId, ContentHash = p.ContentHash },
                                                              cancellationToken: cancellationToken)).Select(p => (p.RuleSourceId.Value, p.ContentHash)).ToHashSet();

        var catalogVersion = await _catalogSource.GetCatalogVersionAsync(cancellationToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var proposals = new List<RuleProposal>();
        int changed = 0, unreachable = 0;

        foreach (var source in sources)
        {
            source.LastFetchDate = now;

            string content;

            try
            {
                content = ContentNormalizer.Normalize(await _fetcher.FetchAsync(source.Url, cancellationToken), RuleSource.ContentMaxLength);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                source.Status = RuleSourceStatus.Unreachable;
                source.LastError = SecretRedactor.RedactAndTrim(ex.Message, 500);
                unreachable++;
                continue;
            }

            var hash = WebhookSignatures.Sha256(content);

            source.LastError = null;

            if (source.ContentHash == null || source.ContentHash == hash)
            {
                source.Status = source.ContentHash == null ? RuleSourceStatus.Unchanged : source.Status == RuleSourceStatus.Changed ? RuleSourceStatus.Changed : RuleSourceStatus.Unchanged;
                source.ContentHash = hash;
                source.Content = content;
                continue;
            }

            changed++;

            if (!proposed.Contains((source.Id, hash)))
            {
                var (removed, added) = TextDiff.Lines(source.Content, content, _diffLines);

                proposals.Add(new RuleProposal
                {
                    Title = $"{source.Name}: content changed",
                    Store = source.Store,
                    RuleSourceId = source.Id,
                    ContentHash = hash,
                    PreviousContentHash = source.ContentHash,
                    Status = RuleProposalStatus.Pending,
                    PreparedBy = RuleProposalPreparer.Watcher,
                    SourceExcerptBefore = Cut(string.Join('\n', removed)),
                    SourceExcerptAfter = Cut(string.Join('\n', added)),
                    CatalogVersionFrom = catalogVersion,
                    CreationDate = now,
                });
            }

            source.Status = RuleSourceStatus.Changed;
            source.PreviousContentHash = source.ContentHash;
            source.ContentHash = hash;
            source.Content = content;
            source.LastChangeDate = now;
        }

        if (sources.Count > 0)
            await _sourceRepository.UpdateAsync(sources, cancellationToken, s => s.LastFetchDate, s => s.Status, s => s.LastError, s => s.ContentHash, s => s.PreviousContentHash, s => s.Content, s => s.LastChangeDate);

        if (proposals.Count > 0)
        {
            try
            {
                await _proposalRepository.AddRangeAsync(proposals, cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                _logger.Warning("Rule proposals could not be saved (another run saved them): {Error}", ex.Message);
                proposals.Clear();
            }
        }

        foreach (var proposal in proposals)
        {
            await _notificationDispatcher.DispatchAsync(new OrgNotification
            {
                Kind = NotificationType.RuleProposal,
                PlatformAdmins = true,
                TextKey = MessageKey.NotificationRuleProposal,
                TextArgs = [sources.First(s => s.Id == proposal.RuleSourceId).Name],
                Data = new { proposalId = proposal.Id },
                RelatedEntity = NotificationEntity.RuleProposal,
                RelatedEntityId = proposal.Id.ToString(CultureInfo.InvariantCulture),
                ActionLink = $"/platform/rules/proposals/{proposal.Id}",
            }, cancellationToken);
        }

        return new RuleWatchResult(sources.Count, changed, unreachable, proposals.Count);
    }

    private static string Cut(string text) => text.Length > RuleProposal.ExcerptMaxLength ? text[..RuleProposal.ExcerptMaxLength] : text;
}
