using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Mockups.CreateMockupTemplate;

/// <summary>
/// Creates a mockup template for an app from a built in starter (design and sample captions) or as a copy of another
/// template of the organization (design, slots and optionally captions). Returns the template id.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record CreateMockupTemplateCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App the template belongs to.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Name, unique within the app.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Locales of the template.
    /// </summary>
    public List<string> Locales { get; set; }

    /// <summary>
    /// Primary locale (one of <see cref="Locales"/>).
    /// </summary>
    public string PrimaryLocale { get; set; }

    /// <summary>
    /// Number of slots; null for the count of the starter or of the copied template.
    /// </summary>
    public int? SlotCount { get; set; }

    /// <summary>
    /// Built in starter to start from; null for the default starter. Ignored when copying.
    /// </summary>
    public string StarterKey { get; set; }

    /// <summary>
    /// Template of the organization to copy instead of a starter.
    /// </summary>
    public int? CopyFromTemplateId { get; set; }

    /// <summary>
    /// Copy the captions too (of the locales both templates have).
    /// </summary>
    public bool CopyCaptions { get; set; }
}
