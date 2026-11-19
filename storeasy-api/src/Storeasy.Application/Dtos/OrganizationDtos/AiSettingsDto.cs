namespace Storeasy.Application.Dtos.OrganizationDtos;

/// <summary>
/// AI settings of an organization. The API key itself is never returned: only whether one is set, when, by whom and its
/// last four characters.
/// </summary>
public class AiSettingsDto
{
    /// <summary>
    /// Whether an Anthropic API key is saved.
    /// </summary>
    public bool HasApiKey { get; set; }

    /// <summary>
    /// Last four characters of the key; null when none is set.
    /// </summary>
    public string ApiKeyLastFour { get; set; }

    /// <summary>
    /// UTC instant the key was saved; null when none is set.
    /// </summary>
    public DateTime? ApiKeySetDate { get; set; }

    /// <summary>
    /// Who saved the key; null when none is set.
    /// </summary>
    public string ApiKeySetByUserName { get; set; }

    /// <summary>
    /// Model the reply drafts are written with (configuration, <c>Storeasy:Ai:Model</c>).
    /// </summary>
    public string Model { get; set; }

    /// <summary>
    /// Only in the answer of saving a key: true when Anthropic accepted it, false when it could not be checked (the key
    /// is saved anyway); null otherwise.
    /// </summary>
    public bool? ApiKeyVerified { get; set; }
}
