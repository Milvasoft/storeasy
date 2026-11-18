using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.TrustedPublishers.DeleteTrustedPublisher;

/// <summary>
/// Removes a trusted publisher rule; upload tokens it issued expire on their own within 15 minutes.
/// </summary>
[OrgAuth(OrgPermission.ManageConnections)]
public record DeleteTrustedPublisherCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Rule id.
    /// </summary>
    public int Id { get; set; }
}
