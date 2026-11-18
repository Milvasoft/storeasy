namespace Storeasy.Application.Dtos.ContentDtos;

/// <summary>
/// App Review details of a version, compared with the app's saved answers.
/// </summary>
public class ReviewInfoDto
{
    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Version.
    /// </summary>
    public string VersionId { get; set; }

    /// <summary>
    /// Contact first name.
    /// </summary>
    public string ContactFirstName { get; set; }

    /// <summary>
    /// Contact last name.
    /// </summary>
    public string ContactLastName { get; set; }

    /// <summary>
    /// Contact e-mail.
    /// </summary>
    public string ContactEmail { get; set; }

    /// <summary>
    /// Contact phone.
    /// </summary>
    public string ContactPhone { get; set; }

    /// <summary>
    /// Whether sign in is required.
    /// </summary>
    public bool DemoAccountRequired { get; set; }

    /// <summary>
    /// Demo account name.
    /// </summary>
    public string DemoAccountName { get; set; }

    /// <summary>
    /// Whether a demo password is sealed in the saved answers (never returned).
    /// </summary>
    public bool DemoPasswordSet { get; set; }

    /// <summary>
    /// Whether the store has a demo password.
    /// </summary>
    public bool StorePasswordSet { get; set; }

    /// <summary>
    /// Notes for the reviewer.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Whether the store differs from the saved answers.
    /// </summary>
    public bool StoreDiffers { get; set; }
}
