using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.TestFlight.CreateBetaGroup;

/// <summary>
/// Creates a TestFlight beta group (a store job); returns the job id.
/// </summary>
[OrgAuth(OrgPermission.ReleaseTest)]
public record CreateBetaGroupCommand : ICommand<long>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Internal group (App Store Connect users) or external.
    /// </summary>
    public bool IsInternal { get; set; }

    /// <summary>
    /// Public link on (external groups).
    /// </summary>
    public bool PublicLinkEnabled { get; set; }

    /// <summary>
    /// Public link limit.
    /// </summary>
    public int? PublicLinkLimit { get; set; }

    /// <summary>
    /// Limit on.
    /// </summary>
    public bool PublicLinkLimitEnabled { get; set; }
}
