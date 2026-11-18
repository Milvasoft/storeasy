using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.OrganizationDtos;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Features.Organizations.GetMemberList;

/// <summary>
/// Handles <see cref="GetMemberListQuery"/>.
/// </summary>
/// <param name="memberRepository"></param>
/// <param name="invitationRepository"></param>
/// <param name="organizationAccess"></param>
/// <param name="timeProvider"></param>
public class GetMemberListQueryHandler(IStoreasyRepositoryBase<OrganizationMember> memberRepository,
                                       IStoreasyRepositoryBase<OrganizationInvitation> invitationRepository,
                                       IOrganizationAccess organizationAccess,
                                       TimeProvider timeProvider) : IInterceptable, IQueryHandler<GetMemberListQuery, MemberListDto>
{
    private readonly IStoreasyRepositoryBase<OrganizationMember> _memberRepository = memberRepository;
    private readonly IStoreasyRepositoryBase<OrganizationInvitation> _invitationRepository = invitationRepository;
    private readonly IOrganizationAccess _organizationAccess = organizationAccess;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public async Task<Response<MemberListDto>> Handle(GetMemberListQuery request, CancellationToken cancellationToken)
    {
        var members = await _memberRepository.GetAllAsync(m => m.OrganizationId == request.OrganizationId,
                                                          projection: OrganizationMemberDto.Projection,
                                                          cancellationToken: cancellationToken);

        List<OrganizationInvitationDto> invitations = [];

        if (_organizationAccess.CurrentRole.HasValue && OrgRoleRules.Allows(_organizationAccess.CurrentRole.Value, OrgPermission.ManageMembers))
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            invitations = await _invitationRepository.GetAllAsync(i => i.OrganizationId == request.OrganizationId && i.AcceptDate == null && i.RevokeDate == null && i.ExpiryDate > now,
                                                                  projection: OrganizationInvitationDto.Projection,
                                                                  cancellationToken: cancellationToken);
        }

        return Response<MemberListDto>.Success(new MemberListDto
        {
            Members = [.. members.OrderBy(m => m.Role).ThenBy(m => m.Email)],
            Invitations = [.. invitations.OrderByDescending(i => i.CreationDate)],
        });
    }
}
