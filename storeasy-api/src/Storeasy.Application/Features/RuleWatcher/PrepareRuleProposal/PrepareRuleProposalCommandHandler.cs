using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Rules;

namespace Storeasy.Application.Features.RuleWatcher.PrepareRuleProposal;

/// <summary>
/// Handles <see cref="PrepareRuleProposalCommand"/>.
/// </summary>
/// <param name="ProposalRepository"></param>
/// <param name="CurrentUser"></param>
[Log]
public record PrepareRuleProposalCommandHandler(IStoreasyRepositoryBase<RuleProposal> ProposalRepository, ICurrentUser CurrentUser) : IInterceptable, ICommandHandler<PrepareRuleProposalCommand, int>
{
    private readonly IStoreasyRepositoryBase<RuleProposal> _proposalRepository = ProposalRepository;
    private readonly ICurrentUser _currentUser = CurrentUser;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(PrepareRuleProposalCommand request, CancellationToken cancellationToken)
    {
        var proposal = await _proposalRepository.GetFirstOrDefaultAsync(p => p.Id == request.ProposalId,
                                                                        projection: p => new { p.Id, p.Status },
                                                                        cancellationToken: cancellationToken);

        if (proposal == null)
            return Response<int>.Error(0, MessageKey.RuleProposalNotFound);

        var updated = await _proposalRepository.ExecuteUpdateAsync(p => p.Id == proposal.Id && p.Status == RuleProposalStatus.Pending,
                                                                   new SetPropertyBuilder<RuleProposal>().SetPropertyValue(p => p.RuleIds, request.RuleIds.Select(r => r.Trim()).Distinct().ToList())
                                                                                                         .SetPropertyValue(p => p.CatalogPatch, request.CatalogPatch)
                                                                                                         .SetPropertyValue(p => p.PreparerNote, request.Note)
                                                                                                         .SetPropertyValue(p => p.PreparedBy, RuleProposalPreparer.Mcp)
                                                                                                         .SetPropertyValue(p => p.PreparedByUserName, _currentUser.UserName),
                                                                   cancellationToken);

        if (updated == 0)
            return Response<int>.Error(0, MessageKey.RuleProposalDecided);

        return Response<int>.Success(proposal.Id);
    }
}
