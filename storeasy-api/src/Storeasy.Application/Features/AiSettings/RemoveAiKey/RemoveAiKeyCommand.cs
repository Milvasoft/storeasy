using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.AiSettings.RemoveAiKey;

/// <summary>
/// Removes the organization's Anthropic API key; "write with AI" is off until a key is saved again. Drafts already written
/// stay. Removing when no key is set succeeds and changes nothing.
/// </summary>
[OrgAuth(OrgPermission.ManageConnections)]
public record RemoveAiKeyCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }
}
