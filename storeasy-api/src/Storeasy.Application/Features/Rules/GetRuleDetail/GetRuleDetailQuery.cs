using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.RuleDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Rules;

namespace Storeasy.Application.Features.Rules.GetRuleDetail;

/// <summary>
/// Reads one catalog rule with its sources, conflicts, fallback chain and the organization's apps matched against it.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetRuleDetailQuery : IQuery<RuleDetailDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Rule id (apple.screenshots.ipad-13...).
    /// </summary>
    public string RuleId { get; set; }
}
