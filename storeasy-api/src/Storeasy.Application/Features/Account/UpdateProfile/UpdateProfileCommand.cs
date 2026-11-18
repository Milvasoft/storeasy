using Milvasoft.Components.CQRS.Command;

namespace Storeasy.Application.Features.Account.UpdateProfile;

/// <summary>
/// Updates the caller's own name, surname and interface language. The account is always the caller's (from the token).
/// Returns the user id.
/// </summary>
public record UpdateProfileCommand : ICommand<int>
{
    /// <summary>
    /// Name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Surname (optional).
    /// </summary>
    public string Surname { get; set; }

    /// <summary>
    /// Preferred interface language, one of the instance's languages (tr-TR, en-US); null for the browser's.
    /// </summary>
    public string Language { get; set; }
}
