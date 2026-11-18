namespace Storeasy.Application.Dtos.IntakeDtos;

/// <summary>
/// Answer of opening an upload session (tools/CONTRACT.md 3.2): a new session, a resumed one or a duplicate.
/// </summary>
public class ArtifactUploadDto
{
    /// <summary>
    /// Session id.
    /// </summary>
    public long SessionId { get; set; }

    /// <summary>
    /// Whether the same file was already received for the app (no URLs then).
    /// </summary>
    public bool Duplicate { get; set; }

    /// <summary>
    /// Whether an open session of the same file was resumed.
    /// </summary>
    public bool Resumed { get; set; }

    /// <summary>
    /// Bytes per part (the last part may be smaller).
    /// </summary>
    public long? PartSize { get; set; }

    /// <summary>
    /// Presigned URLs of the parts still missing.
    /// </summary>
    public List<ArtifactPartUrlDto> Parts { get; set; } = [];

    /// <summary>
    /// Parts already in storage.
    /// </summary>
    public List<ArtifactUploadedPartDto> UploadedParts { get; set; } = [];

    /// <summary>
    /// UTC instant the URLs stop working.
    /// </summary>
    public DateTime? UrlExpiryDate { get; set; }

    /// <summary>
    /// Session that received the file first (duplicates).
    /// </summary>
    public long? OriginalSessionId { get; set; }
}
