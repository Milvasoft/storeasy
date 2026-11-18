using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Components.Rest.Request;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.RuleDtos;
using Storeasy.Domain.Rules;

namespace Storeasy.Application.Features.RuleWatcher.GetRuleProposalList;

/// <summary>
/// Handles <see cref="GetRuleProposalListQuery"/>.
/// </summary>
/// <param name="ProposalRepository"></param>
public record GetRuleProposalListQueryHandler(IStoreasyRepositoryBase<RuleProposal> ProposalRepository) : IInterceptable, IListQueryHandler<GetRuleProposalListQuery, RuleProposalListDto>
{
    private readonly IStoreasyRepositoryBase<RuleProposal> _proposalRepository = ProposalRepository;

    /// <inheritdoc/>
    public async Task<ListResponse<RuleProposalListDto>> Handle(GetRuleProposalListQuery request, CancellationToken cancellationToken)
    {
        request.PageNumber ??= 1;
        request.RowCount = request.RowCount.ClampRowCount(20);
        request.Sorting ??= new SortRequest { SortBy = nameof(RuleProposalListDto.Id), Type = Milvasoft.Components.Rest.Enums.SortType.Desc };

        return await _proposalRepository.GetAllAsync(request, condition: p => p.Id > 0, projection: RuleProposalListDto.Projection, cancellationToken: cancellationToken);
    }
}
