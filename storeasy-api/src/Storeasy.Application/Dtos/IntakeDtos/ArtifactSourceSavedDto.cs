namespace Storeasy.Application.Dtos.IntakeDtos;

/// <summary>
/// A saved pull source. <see cref="Secret"/> is set only when it was created or rotated.
/// </summary>
public class ArtifactSourceSavedDto
{
    /// <summary>
    /// Source id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Webhook path.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// The signing secret (shown once).
    /// </summary>
    public string Secret { get; set; }
}
