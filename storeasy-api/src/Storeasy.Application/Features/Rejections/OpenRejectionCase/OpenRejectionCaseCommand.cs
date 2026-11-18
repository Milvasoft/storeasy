using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Rejections.OpenRejectionCase;

/// <summary>
/// Opens a rejection case by hand (for a rejection the webhook and the sync did not report, or a store without either).
/// Returns the case id.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record OpenRejectionCaseCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// storeasy app.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Store of the rejection.
    /// </summary>
    public StoreKind Store { get; set; } = StoreKind.Apple;

    /// <summary>
    /// Rejected version.
    /// </summary>
    public string VersionString { get; set; }

    /// <summary>
    /// Build number, if known.
    /// </summary>
    public string BuildNumber { get; set; }

    /// <summary>
    /// What was rejected.
    /// </summary>
    public RejectionKind Kind { get; set; } = RejectionKind.Rejected;
}
