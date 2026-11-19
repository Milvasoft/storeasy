using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Api.Utils;
using Storeasy.Application.Dtos.AssetDtos;
using Storeasy.Application.Dtos.OrganizationDtos;
using Storeasy.Application.Dtos.StoreDtos;
using Storeasy.Application.Features.AiSettings.GetAiSettings;
using Storeasy.Application.Features.AiSettings.RemoveAiKey;
using Storeasy.Application.Features.AiSettings.SetAiKey;
using Storeasy.Application.Features.AiSettings.SetAiModels;
using Storeasy.Application.Features.Organizations.AcceptInvitation;
using Storeasy.Application.Features.Organizations.ChangeMemberRole;
using Storeasy.Application.Features.Organizations.CreateOrganization;
using Storeasy.Application.Features.Organizations.CreateOrganizationLogoUpload;
using Storeasy.Application.Features.Organizations.CreateOrganizationRole;
using Storeasy.Application.Features.Organizations.DeleteOrganizationRole;
using Storeasy.Application.Features.Organizations.GetInvitationPreview;
using Storeasy.Application.Features.Organizations.GetMemberList;
using Storeasy.Application.Features.Organizations.GetMyOrganizations;
using Storeasy.Application.Features.Organizations.GetOrganizationRoleList;
using Storeasy.Application.Features.Organizations.GetOrganizationSettings;
using Storeasy.Application.Features.Organizations.UpdateOrganizationSettings;
using Storeasy.Application.Features.Organizations.InviteMember;
using Storeasy.Application.Features.Organizations.RemoveMember;
using Storeasy.Application.Features.Organizations.RemoveOrganizationLogo;
using Storeasy.Application.Features.Organizations.RevokeInvitation;
using Storeasy.Application.Features.Organizations.SetOrganizationLogo;
using Storeasy.Application.Features.Organizations.UpdateOrganization;
using Storeasy.Application.Features.Organizations.UpdateOrganizationRole;
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
    /// Renames an organization and optionally changes its slug (owner only; a slug in use is 409).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("organization")]
    public Task<Response<int>> UpdateOrganizationAsync(UpdateOrganizationCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Declares a logo file (PNG, JPEG or WebP, at most 2 MB; owners) and returns the presigned PUT URL for it. Confirm
    /// with <c>PUT organizations/logo</c>.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("logo/upload")]
    [RateLimit(30, 3600, "org-logo-upload")]
    public Task<Response<AssetUploadDto>> CreateLogoUploadAsync(CreateOrganizationLogoUploadCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Makes the uploaded file the organization's logo (owners): checked by its signature, redrawn as a square PNG,
    /// replacing the previous logo. Returns the new logo URL.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("logo")]
    public Task<Response<string>> SetLogoAsync(SetOrganizationLogoCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Removes the organization's logo (owners); the organization shows its initial again.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpDelete("logo")]
    public Task<Response<int>> RemoveLogoAsync(RemoveOrganizationLogoCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

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
    /// Previews an invitation by the token from its link (anonymous): state, organization, inviter, role and its
    /// permissions, masked address and expiry, as much as the state allows. An unknown token is a 404.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("invitation/preview")]
    [AllowAnonymous]
    [RateLimit(30, 3600, "org-invite-preview")]
    public Task<Response<InvitationPreviewDto>> GetInvitationPreviewAsync([FromQuery] GetInvitationPreviewQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

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
    /// Reads the retention and language settings (member managers; viewers get 403).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("settings")]
    public Task<Response<OrganizationSettingsDto>> GetSettingsAsync([FromQuery] GetOrganizationSettingsQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Changes the audit log retention and the default language (owners). Only the fields sent change.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("settings")]
    public Task<Response<int>> UpdateSettingsAsync(UpdateOrganizationSettingsCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Reads the AI settings: whether an Anthropic API key is set (never the key), its last four characters and the model.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("ai-settings")]
    public Task<Response<AiSettingsDto>> GetAiSettingsAsync([FromQuery] GetAiSettingsQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Saves or replaces the Anthropic API key (checked with Anthropic first, sealed, never returned).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("ai-settings/key")]
    [RateLimit(30, 3600, "org-ai-key")]
    public Task<Response<AiSettingsDto>> SetAiKeyAsync(SetAiKeyCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Removes the Anthropic API key.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpDelete("ai-settings/key")]
    public Task<Response<int>> RemoveAiKeyAsync(RemoveAiKeyCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Chooses the model of each AI feature (reply drafts, pre-reviews) from the fixed list; needs a saved key.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("ai-settings/models")]
    public Task<Response<AiSettingsDto>> SetAiModelsAsync(SetAiModelsCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Lists the organization's roles: the four locked seeded roles, then the custom roles, with member and pending
    /// invitation counts.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpGet("roles")]
    public Task<Response<List<OrgRoleDto>>> GetRolesAsync([FromQuery] GetOrganizationRoleListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Defines a custom role (permissions the caller holds only). Returns the role id.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPost("role")]
    public Task<Response<int>> CreateRoleAsync(CreateOrganizationRoleCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Renames a custom role and replaces its permissions; its members get them on their next request.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPut("role")]
    public Task<Response<int>> UpdateRoleAsync(UpdateOrganizationRoleCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Deletes a custom role nobody holds.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpDelete("role")]
    public Task<Response<int>> DeleteRoleAsync(DeleteOrganizationRoleCommand request, CancellationToken cancellation) => _mediator.Send(request, cancellation);

    /// <summary>
    /// Pages through the organization's audit log, newest first.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    [HttpPatch("audit-logs")]
    public Task<ListResponse<StoreAuditLogDto>> GetAuditLogsAsync(GetStoreAuditLogListQuery request, CancellationToken cancellation) => _mediator.Send(request, cancellation);
}
