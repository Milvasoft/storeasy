using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.RegistryDtos;

namespace Storeasy.Application.Features.Registries.GetTokenReminderList;

/// <summary>
/// The registry token reminders of the organization (label, registry, where the token is used, expiry and how many days
/// are left), soonest expiry first. Tokens themselves are never stored.
/// </summary>
[OrgAuth(OrgPermission.View)]
public record GetTokenReminderListQuery : IQuery<List<TokenReminderDto>>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }
}
