using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.OrganizationDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Features.Organizations.GetInvitationPreview;

/// <summary>
/// Handles <see cref="GetInvitationPreviewQuery"/>: one projection by token hash, then only what the invitation's state
/// may show (see <see cref="InvitationPreviewDto"/>). An invitation of a deleted organization reads as revoked.
/// </summary>
/// <param name="invitationRepository"></param>
/// <param name="timeProvider"></param>
/// <param name="fileStorage"></param>
public class GetInvitationPreviewQueryHandler(IStoreasyRepositoryBase<OrganizationInvitation> invitationRepository, TimeProvider timeProvider, IFileStorage fileStorage) : IInterceptable, IQueryHandler<GetInvitationPreviewQuery, InvitationPreviewDto>
{
    private readonly IStoreasyRepositoryBase<OrganizationInvitation> _invitationRepository = invitationRepository;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly IFileStorage _fileStorage = fileStorage;

    /// <inheritdoc/>
    public async Task<Response<InvitationPreviewDto>> Handle(GetInvitationPreviewQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return Response<InvitationPreviewDto>.Error(null, MessageKey.InvitationInvalid);

        var tokenHash = SecureTokens.Hash(request.Token.Trim());

        var invitation = await _invitationRepository.GetFirstOrDefaultAsync(i => i.TokenHash == tokenHash,
                                                                            projection: i => new
                                                                            {
                                                                                i.Email,
                                                                                i.Role,
                                                                                i.CustomRoleId,
                                                                                CustomRoleName = i.CustomRole != null ? i.CustomRole.Name : null,
                                                                                CustomPermissions = i.CustomRole != null ? i.CustomRole.Permissions : null,
                                                                                i.ExpiryDate,
                                                                                i.AcceptDate,
                                                                                i.RevokeDate,
                                                                                OrganizationDeleted = i.Organization.IsDeleted,
                                                                                OrganizationName = i.Organization.Name,
                                                                                OrganizationSlug = i.Organization.Slug,
                                                                                OrganizationLogoKey = i.Organization.LogoKey,
                                                                                InviterName = i.Inviter.Surname == null ? i.Inviter.Name : i.Inviter.Name + " " + i.Inviter.Surname,
                                                                            },
                                                                            cancellationToken: cancellationToken);

        if (invitation == null)
            return Response<InvitationPreviewDto>.Error(null, MessageKey.InvitationInvalid);

        var status = invitation.RevokeDate != null || invitation.OrganizationDeleted
            ? InvitationStatus.Revoked
            : invitation.AcceptDate != null
                ? InvitationStatus.Accepted
                : invitation.ExpiryDate <= _timeProvider.GetUtcNow().UtcDateTime ? InvitationStatus.Expired : InvitationStatus.Pending;

        var preview = new InvitationPreviewDto { Status = status };

        if (status == InvitationStatus.Revoked)
            return Response<InvitationPreviewDto>.Success(preview);

        preview.OrganizationName = invitation.OrganizationName;
        preview.OrganizationSlug = invitation.OrganizationSlug;
        preview.OrganizationLogoUrl = _fileStorage.LogoUrl(invitation.OrganizationLogoKey);

        if (status == InvitationStatus.Accepted)
            return Response<InvitationPreviewDto>.Success(preview);

        preview.InviterName = invitation.InviterName;
        preview.MaskedEmail = EmailMasking.Mask(invitation.Email);
        preview.ExpiryDate = invitation.ExpiryDate;

        if (status == InvitationStatus.Pending)
        {
            preview.Role = invitation.Role;
            preview.CustomRoleName = invitation.CustomRoleName;
            preview.Permissions = [.. OrgAccess.Of(invitation.Role, invitation.CustomRoleId, invitation.CustomPermissions).Permissions.Order()];
        }

        return Response<InvitationPreviewDto>.Success(preview);
    }
}
