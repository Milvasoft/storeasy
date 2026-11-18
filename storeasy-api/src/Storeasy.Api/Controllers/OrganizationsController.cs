using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.OrganizationDtos;
using Storeasy.Application.Dtos.StoreDtos;
using Storeasy.Application.Features.Organizations.AcceptInvitation;
using Storeasy.Application.Features.Organizations.ChangeMemberRole;
using Storeasy.Application.Features.Organizations.CreateOrganization;
using Storeasy.Application.Features.Organizations.GetMemberList;
using Storeasy.Application.Features.Organizations.GetMyOrganizations;
using Storeasy.Application.Features.Organizations.InviteMember;
using Storeasy.Application.Features.Organizations.RemoveMember;
using Storeasy.Application.Features.Organizations.RevokeInvitation;
using Storeasy.Application.Features.Organizations.UpdateOrganization;
using Storeasy.Application.Features.StoreAuditLogs.GetStoreAuditLogList;
using Storeasy.Application.Utils.Constants;

namespace Storeasy.Api.Controllers;

/// <summary>
/// Organization, member, invitation and audit log endpoints. Any signed in user may call them; the organization role
/// checks run in the request pipeline (<c>[OrgAuth]</c> on each request), the same for the web app and MCP.
/// </summary>
[ApiController]
[Route(GlobalConstant.FullRoute)]
[ApiVersion(GlobalConstant.CurrentApiVersion)]
[ApiExplorerSettings(GroupName = "v1.0")]
public class OrganizationsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Lists the current user's organizations with the user's role and permissions in each.
    /// </summary>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("mine")]
    public Task<Response<List<MyOrganizationDto>>> GetMyOrganizationsAsync(CancellationToken cancellation) => _mediator.Send(new GetMyOrganizationsQuery(), cancellation);

    /// <summary>
    /// Creates an organization; the caller becomes its owner. Retry safe with an <c>Idempotency-Key</c> header.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("organization")]
    [Idempotent]
    public Task<Response<int>> CreateOrganizationAsync(CreateOrganizationCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Renames an organization (owner only).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("organization")]
    public Task<Response<int>> UpdateOrganizationAsync(UpdateOrganizationCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Lists members and, for member managers, pending invitations.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("members")]
    public Task<Response<MemberListDto>> GetMembersAsync([FromQuery] GetMemberListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Invites an e-mail address. The response carries the one time token for the accept link.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("invitation")]
    [Idempotent]
    [RateLimit(30, 3600, "org-invite")]
    public Task<Response<InvitationCreatedDto>> InviteMemberAsync(InviteMemberCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Revokes a pending invitation.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpDelete("invitation")]
    public Task<Response<int>> RevokeInvitationAsync(RevokeInvitationCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Accepts an invitation with the token from the link. Returns the organization id.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("invitation/accept")]
    [RateLimit(20, 3600, "org-invite-accept")]
    public Task<Response<int>> AcceptInvitationAsync(AcceptInvitationCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Changes a member's role.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("member/role")]
    public Task<Response<int>> ChangeMemberRoleAsync(ChangeMemberRoleCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Removes a member, or leaves the organization when the id is the caller's own membership.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpDelete("member")]
    public Task<Response<int>> RemoveMemberAsync(RemoveMemberCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Pages through the organization's audit log, newest first.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPatch("audit-logs")]
    public Task<ListResponse<StoreAuditLogDto>> GetAuditLogsAsync(GetStoreAuditLogListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
