using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.RuleDtos;
using Storeasy.Domain.Rules;

namespace Storeasy.Application.Features.RuleWatcher.GetRuleProposalDetail;

/// <summary>
/// Handles <see cref="GetRuleProposalDetailQuery"/>.
/// </summary>
/// <param name="ProposalRepository"></param>
/// <param name="SourceRepository"></param>
/// <param name="CatalogSource"></param>
public record GetRuleProposalDetailQueryHandler(IStoreasyRepositoryBase<RuleProposal> ProposalRepository,
                                                IStoreasyRepositoryBase<RuleSource> SourceRepository,
                                                IRulesCatalogSource CatalogSource) : IInterceptable, IQueryHandler<GetRuleProposalDetailQuery, RuleProposalDetailDto>
{
    private readonly IStoreasyRepositoryBase<RuleProposal> _proposalRepository = ProposalRepository;
    private readonly IStoreasyRepositoryBase<RuleSource> _sourceRepository = SourceRepository;
    private readonly IRulesCatalogSource _catalogSource = CatalogSource;

    /// <inheritdoc/>
    public async Task<Response<RuleProposalDetailDto>> Handle(GetRuleProposalDetailQuery request, CancellationToken cancellationToken)
    {
        var proposal = await _proposalRepository.GetFirstOrDefaultAsync(p => p.Id == request.ProposalId,
                                                                        projection: p => new RuleProposalDetailDto
                                                                        {
                                                                            Id = p.Id,
                                                                            Title = p.Title,
                                                                            Store = p.Store,
                                                                            RuleIds = p.RuleIds,
                                                                            RuleSourceId = p.RuleSourceId,
                                                                            Status = p.Status,
                                                                            PreparedBy = p.PreparedBy,
                                                                            PreparedByUserName = p.PreparedByUserName,
                                                                            CatalogVersionFrom = p.CatalogVersionFrom,
                                                                            CatalogVersionTo = p.CatalogVersionTo,
                                                                            CreationDate = p.CreationDate,
                                                                            ContentHash = p.ContentHash,
                                                                            PreviousContentHash = p.PreviousContentHash,
                                                                            SourceExcerptBefore = p.SourceExcerptBefore,
                                                                            SourceExcerptAfter = p.SourceExcerptAfter,
                                                                            CatalogPatch = p.CatalogPatch,
                                                                            PreparerNote = p.PreparerNote,
                                                                            RejectReason = p.RejectReason,
                                                                            DecisionNote = p.DecisionNote,
                                                                            DecidedByUserName = p.DecidedByUserName,
                                                                            DecidedDate = p.DecidedDate,
                                                                            NotifiedOrganizationCount = p.NotifiedOrganizationCount,
                                                                        },
                                                                        cancellationToken: cancellationToken);

        if (proposal == null)
            return Response<RuleProposalDetailDto>.Error(null, MessageKey.RuleProposalNotFound);

        if (proposal.RuleSourceId.HasValue)
        {
            var source = await _sourceRepository.GetFirstOrDefaultAsync(s => s.Id == proposal.RuleSourceId,
                                                                        projection: s => new { s.Name, s.Url },
                                                                        cancellationToken: cancellationToken);

            proposal.SourceName = source?.Name;
            proposal.SourceUrl = source?.Url;
        }

        if (proposal.RuleIds?.Count > 0)
            proposal.Impact = [.. await _catalogSource.FindAffectedAppsAsync(proposal.RuleIds, cancellationToken)];

        return Response<RuleProposalDetailDto>.Success(proposal);
    }
}
