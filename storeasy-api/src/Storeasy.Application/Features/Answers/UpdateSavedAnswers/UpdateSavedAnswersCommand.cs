using Milvasoft.Components.CQRS.Command;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Answers.UpdateSavedAnswers;

/// <summary>
/// Writes one saved answer document of an app (the typed document of its kind; unknown members are refused). The demo account password of the review login is sealed and never returned; leaving it empty keeps the sealed one.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record UpdateSavedAnswersCommand : ICommand<int>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// App id.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Which document.
    /// </summary>
    public SavedAnswerKind Key { get; set; }

    /// <summary>
    /// The document.
    /// </summary>
    public System.Text.Json.JsonElement Answers { get; set; }

    /// <summary>
    /// Demo account password (review login only); empty keeps the sealed one.
    /// </summary>
    [LogMasked]
    public string DemoPassword { get; set; }

    /// <summary>
    /// Removes the sealed demo account password.
    /// </summary>
    public bool RemoveDemoAccountLogin { get; set; }
}
