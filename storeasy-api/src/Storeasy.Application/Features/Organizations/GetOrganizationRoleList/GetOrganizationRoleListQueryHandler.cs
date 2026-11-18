using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.OrganizationDtos;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Features.Organizations.GetOrganizationRoleList;

/// <summary>
/// Handles <see cref="GetOrganizationRoleListQuery"/>. Members and pending invitations are few, so they are counted in
/// memory from one projection each.
/// </summary>
/// <param name="roleRepository"></param>
/// <param name="memberRepository"></param>
/// <param name="invitationRepository"></param>
/// <param name="timeProvider"></param>
public class GetOrganizationRoleListQueryHandler(IStoreasyRepositoryBase<OrganizationRole> roleRepository,
                                                 IStoreasyRepositoryBase<OrganizationMember> memberRepository,
                                                 IStoreasyRepositoryBase<OrganizationInvitation> invitationRepository,
                                                 TimeProvider timeProvider) : IInterceptable, IQueryHandler<GetOrganizationRoleListQuery, List<OrgRoleDto>>
{
    private static readonly OrgRole[] _seededRoles = [OrgRole.Owner, OrgRole.Admin, OrgRole.Editor, OrgRole.Viewer];

    private readonly IStoreasyRepositoryBase<OrganizationRole> _roleRepository = roleRepository;
    private readonly IStoreasyRepositoryBase<OrganizationMember> _memberRepository = memberRepository;
    private readonly IStoreasyRepositoryBase<OrganizationInvitation> _invitationRepository = invitationRepository;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public async Task<Response<List<OrgRoleDto>>> Handle(GetOrganizationRoleListQuery request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var members = await _memberRepository.GetAllAsync(m => m.OrganizationId == request.OrganizationId,
                                                          projection: m => new OrganizationMember { Role = m.Role, CustomRoleId = m.CustomRoleId },
                                                          cancellationToken: cancellationToken);

        var invitations = await _invitationRepository.GetAllAsync(i => i.OrganizationId == request.OrganizationId && i.AcceptDate == null && i.RevokeDate == null && i.ExpiryDate > now,
                                                                  projection: i => new OrganizationInvitation { Role = i.Role, CustomRoleId = i.CustomRoleId },
                                                                  cancellationToken: cancellationToken);

        var customRoles = await _roleRepository.GetAllAsync(r => r.OrganizationId == request.OrganizationId,
                                                            projection: r => new OrgRoleDto
                                                            {
                                                                Id = r.Id,
                                                                Role = OrgRole.Custom,
                                                                Name = r.Name,
                                                                Description = r.Description,
                                                                Permissions = r.Permissions,
                                                                CreationDate = r.CreationDate,
                                                            },
                                                            cancellationToken: cancellationToken);

        var roles = _seededRoles.Select(role => new OrgRoleDto
        {
            Role = role,
            Permissions = OrgRoleRules.PermissionsOf(role),
            MemberCount = members.Count(m => m.Role == role),
            InvitationCount = invitations.Count(i => i.Role == role),
            IsLocked = true,
        }).ToList();

        foreach (var role in customRoles.OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            role.Permissions = OrgRoleRules.NormalizeCustom(role.Permissions);
            role.MemberCount = members.Count(m => m.Role == OrgRole.Custom && m.CustomRoleId == role.Id);
            role.InvitationCount = invitations.Count(i => i.Role == OrgRole.Custom && i.CustomRoleId == role.Id);

            roles.Add(role);
        }

        return Response<List<OrgRoleDto>>.Success(roles);
    }
}
