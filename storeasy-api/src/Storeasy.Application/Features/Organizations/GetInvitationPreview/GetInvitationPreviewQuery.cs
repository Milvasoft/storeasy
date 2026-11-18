using Milvasoft.Components.CQRS.Query;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.OrganizationDtos;

namespace Storeasy.Application.Features.Organizations.GetInvitationPreview;

/// <summary>
/// Previews an invitation by its token (anonymous). An unknown token is a 404 indistinguishable from a malformed one;
/// tokens are long random values stored as hashes only, so they cannot be enumerated.
/// </summary>
public record GetInvitationPreviewQuery : IQuery<InvitationPreviewDto>
{
    /// <summary>
    /// Token from the invitation link.
    /// </summary>
    [LogMasked]
    public string Token { get; set; }
}
