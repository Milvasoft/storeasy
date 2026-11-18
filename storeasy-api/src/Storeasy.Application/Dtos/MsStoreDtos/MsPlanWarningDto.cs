namespace Storeasy.Application.Dtos.MsStoreDtos;

/// <summary>
/// A remark of a plan. <see cref="Key"/> is a message key; <see cref="Message"/> is its text in the caller's language.
/// </summary>
public class MsPlanWarningDto
{
    /// <summary>
    /// Message key.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Localized text.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Field, if one.
    /// </summary>
    public string Field { get; set; }

    /// <summary>
    /// Language, if one.
    /// </summary>
    public string Locale { get; set; }
}
