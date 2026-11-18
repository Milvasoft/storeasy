using Milvasoft.Components.CQRS.Query;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.AssistedStepDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.AssistedSteps.GetAssistedStepDetail;

/// <summary>
/// Reads one assisted step with its checklist, store page, known traps and the prepared saved answers (never a password).
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetAssistedStepDetailQuery : IQuery<AssistedStepDetailDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Step id.
    /// </summary>
    public int StepId { get; set; }
}
