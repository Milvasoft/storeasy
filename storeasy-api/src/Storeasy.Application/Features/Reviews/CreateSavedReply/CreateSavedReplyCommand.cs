using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Reviews.CreateSavedReply;

/// <summary>
/// Adds a saved reply (one text per language; variables <c>{ad}</c>/<c>{name}</c>, <c>{uygulama}</c>/<c>{app}</c>,
/// <c>{version}</c> and its Turkish form are filled when it is picked). Returns its id.
/// </summary>
[OrgAuth(OrgPermission.ReplyReviews)]
public record CreateSavedReplyCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Text per language.
    /// </summary>
    public List<LocalizedText> Variants { get; set; } = [];

    /// <summary>
    /// Apps it is suggested for (empty: every app).
    /// </summary>
    public List<int> AppIds { get; set; } = [];
}
