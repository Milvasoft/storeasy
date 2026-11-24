using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.EasDtos;

namespace Storeasy.Application.Features.Eas.GetEasSubmission;

/// <summary>
/// One EAS Submit submission with its state, track, logs and error.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetEasSubmissionQuery : IQuery<EasSubmissionDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// storeasy submission id.
    /// </summary>
    public long Id { get; set; }
}
