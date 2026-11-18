using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.NotificationDtos;
using Storeasy.Application.Dtos.RuleDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Rules;

namespace Storeasy.Application.Features.RuleWatcher.ApproveRuleProposal;

/// <summary>
/// Handles <see cref="ApproveRuleProposalCommand"/>. The decision is a conditional update, so two admins deciding at once
/// get one success and one conflict.
/// </summary>
/// <param name="ProposalRepository"></param>
/// <param name="RulesCatalog"></param>
/// <param name="ImpactFinder"></param>
/// <param name="NotificationDispatcher"></param>
/// <param name="CurrentUser"></param>
/// <param name="TimeProvider"></param>
[Log]
[UserActivityTrack(UserActivity.ApproveRuleProposal)]
public record ApproveRuleProposalCommandHandler(IStoreasyRepositoryBase<RuleProposal> ProposalRepository,
                                               IRulesCatalog RulesCatalog,
                                               IRuleImpactFinder ImpactFinder,
                                               INotificationDispatcher NotificationDispatcher,
                                               ICurrentUser CurrentUser,
                                               TimeProvider TimeProvider) : IInterceptable, ICommandHandler<ApproveRuleProposalCommand, RuleProposalDecisionDto>
{
    private readonly IStoreasyRepositoryBase<RuleProposal> _proposalRepository = ProposalRepository;
    private readonly IRulesCatalog _rulesCatalog = RulesCatalog;
    private readonly IRuleImpactFinder _impactFinder = ImpactFinder;
    private readonly INotificationDispatcher _notificationDispatcher = NotificationDispatcher;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<RuleProposalDecisionDto>> Handle(ApproveRuleProposalCommand request, CancellationToken cancellationToken)
    {
        var proposal = await _proposalRepository.GetFirstOrDefaultAsync(p => p.Id == request.ProposalId,
                                                                        projection: p => new RuleProposal { Id = p.Id, Title = p.Title, RuleIds = p.RuleIds, CatalogPatch = p.CatalogPatch },
                                                                        cancellationToken: cancellationToken);

        if (proposal == null)
            return Response<RuleProposalDecisionDto>.Error(null, MessageKey.RuleProposalNotFound);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var decided = await _proposalRepository.ExecuteUpdateAsync(p => p.Id == proposal.Id && p.Status == RuleProposalStatus.Pending,
                                                                   new SetPropertyBuilder<RuleProposal>().SetPropertyValue(p => p.Status, RuleProposalStatus.Approved)
                                                                                                         .SetPropertyValue(p => p.DecidedByUserName, _currentUser.UserName)
                                                                                                         .SetPropertyValue(p => p.DecidedDate, (DateTime?)now)
                                                                                                         .SetPropertyValue(p => p.DecisionNote, request.Note),
                                                                   cancellationToken);

        if (decided == 0)
            return Response<RuleProposalDecisionDto>.Error(null, MessageKey.RuleProposalDecided);

        var ruleIds = proposal.RuleIds ?? [];
        // The catalog is versioned data in the repository (rules/): the approved patch goes in by a pull request and the next
        // catalog version is published with it. The decision records the version it was approved against.
        var version = _rulesCatalog.Current?.Version;

        var organizations = 0;
        var users = 0;

        if (request.Notify && ruleIds.Count > 0)
        {
            var affected = await _impactFinder.FindAsync(ruleIds, cancellationToken);

            foreach (var organization in affected.GroupBy(a => a.OrganizationId))
            {
                var reached = await _notificationDispatcher.DispatchAsync(new OrgNotification
                {
                    OrganizationId = organization.Key,
                    Kind = NotificationType.RuleChanged,
                    Roles = NotificationAudiences.OwnersAndAdmins,
                    TextKey = MessageKey.NotificationRuleChanged,
                    TextArgs = [proposal.Title, string.Join(", ", organization.Select(a => a.AppName).Distinct())],
                    Data = new { proposalId = proposal.Id, ruleIds, apps = organization.Select(a => new { a.AppId, a.AppName, a.RuleId, a.Message }) },
                    RelatedEntity = NotificationEntity.RuleProposal,
                    RelatedEntityId = proposal.Id.ToString(),
                    ActionLink = "/compliance",
                }, cancellationToken);

                organizations++;
                users += reached;
            }
        }

        await _proposalRepository.ExecuteUpdateAsync(proposal.Id,
                                                     new SetPropertyBuilder<RuleProposal>().SetPropertyValue(p => p.CatalogVersionTo, version)
                                                                                           .SetPropertyValue(p => p.NotifiedOrganizationCount, organizations),
                                                     cancellationToken);

        return Response<RuleProposalDecisionDto>.Success(new RuleProposalDecisionDto { CatalogVersion = version, NotifiedOrganizationCount = organizations, NotifiedUserCount = users });
    }
}
