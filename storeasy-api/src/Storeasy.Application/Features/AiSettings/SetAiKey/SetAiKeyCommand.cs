using Milvasoft.Components.CQRS.Command;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.OrganizationDtos;

namespace Storeasy.Application.Features.AiSettings.SetAiKey;

/// <summary>
/// Saves or replaces the organization's Anthropic API key, used to write review reply drafts. The key is checked with
/// Anthropic first (reading the model costs nothing): a key Anthropic rejects is not saved; when Anthropic cannot be
/// reached the key is saved with a warning. It is sealed and stored; it is never returned, logged or audited.
/// </summary>
[OrgAuth(OrgPermission.ManageConnections)]
[VerifiedEmailRequired]
public record SetAiKeyCommand : ICommand<AiSettingsDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// The Anthropic API key (<c>sk-ant-...</c>). Masked in logs.
    /// </summary>
    [LogMasked]
    public string ApiKey { get; set; }
}
