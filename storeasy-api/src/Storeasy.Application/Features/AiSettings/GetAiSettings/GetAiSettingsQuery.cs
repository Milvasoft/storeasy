using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.OrganizationDtos;

namespace Storeasy.Application.Features.AiSettings.GetAiSettings;

/// <summary>
/// Reads the organization's AI settings: whether an Anthropic API key is set (never the key) and the model. Every member
/// reads it, so the reviews page knows whether "write with AI" is available.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetAiSettingsQuery : IQuery<AiSettingsDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }
}
