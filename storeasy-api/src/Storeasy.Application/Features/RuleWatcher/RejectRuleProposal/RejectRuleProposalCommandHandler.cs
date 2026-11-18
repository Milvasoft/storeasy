using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Rules;

namespace Storeasy.Application.Features.RuleWatcher.RejectRuleProposal;

/// <summary>
/// Handles <see cref="RejectRuleProposalCommand"/>.
/// </summary>
/// <param name="ProposalRepository"></param>
/// <param name="CurrentUser"></param>
/// <param name="TimeProvider"></param>
[Log]
[UserActivityTrack(UserActivity.RejectRuleProposal)]
public record RejectRuleProposalCommandHandler(IStoreasyRepositoryBase<RuleProposal> ProposalRepository, ICurrentUser CurrentUser, TimeProvider TimeProvider) : IInterceptable, ICommandHandler<RejectRuleProposalCommand, int>
{
    private readonly IStoreasyRepositoryBase<RuleProposal> _proposalRepository = ProposalRepository;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(RejectRuleProposalCommand request, CancellationToken cancellationToken)
    {
        var proposal = await _proposalRepository.GetFirstOrDefaultAsync(p => p.Id == request.ProposalId,
                                                                        projection: p => new { p.Id },
                                                                        cancellationToken: cancellationToken);

        if (proposal == null)
            return Response<int>.Error(0, MessageKey.RuleProposalNotFound);

        var decided = await _proposalRepository.ExecuteUpdateAsync(p => p.Id == proposal.Id && p.Status == RuleProposalStatus.Pending,
                                                                   new SetPropertyBuilder<RuleProposal>().SetPropertyValue(p => p.Status, RuleProposalStatus.Rejected)
                                                                                                         .SetPropertyValue(p => p.RejectReason, (RuleProposalRejectReason?)request.Reason)
                                                                                                         .SetPropertyValue(p => p.DecisionNote, request.Note)
                                                                                                         .SetPropertyValue(p => p.DecidedByUserName, _currentUser.UserName)
                                                                                                         .SetPropertyValue(p => p.DecidedDate, (DateTime?)_timeProvider.GetUtcNow().UtcDateTime),
                                                                   cancellationToken);

        if (decided == 0)
            return Response<int>.Error(0, MessageKey.RuleProposalDecided);

        return Response<int>.Success(proposal.Id);
    }
}
