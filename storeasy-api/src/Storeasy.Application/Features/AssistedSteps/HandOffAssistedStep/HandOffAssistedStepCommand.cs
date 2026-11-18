using Milvasoft.Components.CQRS.Command;
using Milvasoft.Core.Abstractions.Localization;
using Storeasy.Application.Dtos.AssistedStepDtos;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.AssistedSteps.HandOffAssistedStep;

/// <summary>
/// Hands an assisted step off to a browser agent working in the member's own store session: returns the structured step (store page, checklist, traps, prepared answers, instruction) and marks the step handed off. No password is ever part of it.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record HandOffAssistedStepCommand : ICommand<AssistedStepHandOffDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Step id.
    /// </summary>
    public int StepId { get; set; }
}
