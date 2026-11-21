using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.AsoDtos;

namespace Storeasy.Application.Features.AsoKeywords.GetAsoKeywordCoverage;

/// <summary>
/// Apple's keywords field helper for an app's App Store record, per listing language (read live): which tracked keywords
/// the name, subtitle and 100 character keywords field cover, which words are missing, which words are spent twice and
/// how many characters are left. Only the App Store has a keywords field.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetAsoKeywordCoverageQuery : IQuery<List<AsoKeywordCoverageDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App id.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Only this listing locale; every locale when empty.
    /// </summary>
    public string Locale { get; set; }
}
