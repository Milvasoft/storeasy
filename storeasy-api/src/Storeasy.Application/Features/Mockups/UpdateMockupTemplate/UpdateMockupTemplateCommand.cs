using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.MockupDtos;

namespace Storeasy.Application.Features.Mockups.UpdateMockupTemplate;

/// <summary>
/// Saves a mockup template: name, design, locales, slot count and the raw screenshots of the slots (replaced as a set).
/// <see cref="Version"/> is the version the editor started from; when someone saved in between the save is refused with
/// 409 and nothing is overwritten. Returns the new version.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record UpdateMockupTemplateCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Template id.
    /// </summary>
    public int TemplateId { get; set; }

    /// <summary>
    /// Version the change is based on.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Name, unique within the app.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The design.
    /// </summary>
    public MockupSpec Spec { get; set; }

    /// <summary>
    /// Locales of the template.
    /// </summary>
    public List<string> Locales { get; set; }

    /// <summary>
    /// Primary locale (one of <see cref="Locales"/>).
    /// </summary>
    public string PrimaryLocale { get; set; }

    /// <summary>
    /// Number of slots.
    /// </summary>
    public int SlotCount { get; set; }

    /// <summary>
    /// Raw screenshots of the slots (the whole set).
    /// </summary>
    public List<MockupSlotDto> Slots { get; set; } = [];
}
