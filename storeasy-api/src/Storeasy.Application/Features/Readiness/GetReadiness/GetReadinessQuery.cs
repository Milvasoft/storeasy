using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.ReadinessDtos;

namespace Storeasy.Application.Features.Readiness.GetReadiness;

/// <summary>
/// Checks what is missing before a submission of one app in one store (docs/urun-plani.md 11.1): texts per locale against the catalog limits, required screenshot classes (iPad only when the app supports iPad), build attached and VALID, export compliance, review information, age rating, categories, pricing, saved answers for assisted steps, target API level and the deadlines of the compliance timeline. Reads the store live.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetReadinessQuery : IQuery<ReadinessReportDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App id.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }
}
