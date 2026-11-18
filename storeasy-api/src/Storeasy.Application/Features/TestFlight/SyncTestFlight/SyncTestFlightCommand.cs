using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.TestFlight.SyncTestFlight;

/// <summary>
/// Reads the TestFlight groups, testers, builds and beta texts of an App Store record now (they are also read every six
/// hours). Returns the job id.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record SyncTestFlightCommand : ICommand<long>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App Store record.
    /// </summary>
    public int StoreAppId { get; set; }
}
