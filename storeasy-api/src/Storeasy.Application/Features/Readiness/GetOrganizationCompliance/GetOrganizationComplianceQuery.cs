using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.ReadinessDtos;
using Storeasy.Application.Utils.Content;

namespace Storeasy.Application.Features.Readiness.GetOrganizationCompliance;

/// <summary>
/// The compliance calendar of the organization: the deadlines of every app between From and To (open ended when empty), nearest first.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetOrganizationComplianceQuery : IQuery<List<ComplianceItemDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// First day.
    /// </summary>
    public DateOnly? From { get; set; }

    /// <summary>
    /// Last day.
    /// </summary>
    public DateOnly? To { get; set; }

    /// <summary>
    /// Leave out the done items.
    /// </summary>
    public bool OpenOnly { get; set; }
}
