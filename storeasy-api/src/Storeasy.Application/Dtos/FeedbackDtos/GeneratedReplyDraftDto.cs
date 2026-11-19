namespace Storeasy.Application.Dtos.FeedbackDtos;

/// <summary>
/// A reply draft written with AI and saved as the review's draft. Nothing was sent to the store.
/// </summary>
public class GeneratedReplyDraftDto
{
    /// <summary>
    /// Review.
    /// </summary>
    public long ReviewId { get; set; }

    /// <summary>
    /// The draft text, within the store's limit.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// True when the model's text was longer than the store accepts and was cut after its last full sentence that fits.
    /// </summary>
    public bool Shortened { get; set; }

    /// <summary>
    /// Model that wrote the draft.
    /// </summary>
    public string Model { get; set; }
}
