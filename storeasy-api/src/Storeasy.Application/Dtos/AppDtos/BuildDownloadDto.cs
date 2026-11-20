namespace Storeasy.Application.Dtos.AppDtos;

/// <summary>
/// A short lived presigned download URL of the package storeasy keeps for a build.
/// </summary>
public class BuildDownloadDto
{
    /// <summary>
    /// Build id.
    /// </summary>
    public long BuildId { get; set; }

    /// <summary>
    /// Upload session the package came with.
    /// </summary>
    public long SessionId { get; set; }

    /// <summary>
    /// Presigned GET URL; it saves the file under <see cref="FileName"/>.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// UTC instant the URL stops working.
    /// </summary>
    public DateTime ExpiryDate { get; set; }

    /// <summary>
    /// File name of the package.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Size in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Lower case hex SHA-256 of the file.
    /// </summary>
    public string Sha256 { get; set; }
}
