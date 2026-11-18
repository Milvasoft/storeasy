namespace Storeasy.Application.Dtos.ContentDtos;

/// <summary>
/// One item of a media set.
/// </summary>
public class MediaItemDto
{
    /// <summary>
    /// The store's id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Position, from 1.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// File name.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Width.
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Height.
    /// </summary>
    public int? Height { get; set; }

    /// <summary>
    /// Store checksum (MD5 for the App Store, SHA-256 for Play).
    /// </summary>
    public string Checksum { get; set; }

    /// <summary>
    /// Processing state.
    /// </summary>
    public string State { get; set; }

    /// <summary>
    /// Address of the image (Play).
    /// </summary>
    public string Url { get; set; }
}
