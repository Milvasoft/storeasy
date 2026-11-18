namespace Storeasy.Domain.JsonModels;

/// <summary>
/// A text in one language (saved reply variants, TestFlight "what to test").
/// </summary>
public class LocalizedText
{
    /// <summary>
    /// Locale (e.g. <c>tr</c>, <c>en-US</c>).
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Text.
    /// </summary>
    public string Text { get; set; }
}
