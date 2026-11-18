using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Organizations.CreateOrganization;

/// <summary>
/// Creates an organization; the caller becomes its owner.
/// </summary>
public record CreateOrganizationCommand : ICommand<int>
{
    /// <summary>
    /// Display name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Optional URL key. Derived from <see cref="Name"/> when empty.
    /// </summary>
    public string Slug { get; set; }
}
