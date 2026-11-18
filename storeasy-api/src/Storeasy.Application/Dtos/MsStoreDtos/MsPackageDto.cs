namespace Storeasy.Application.Dtos.MsStoreDtos;

/// <summary>
/// A package of an MSIX app or flight submission.
/// </summary>
public class MsPackageDto
{
    /// <summary>
    /// Store's package id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// File name.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Package version.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Architecture.
    /// </summary>
    public string Architecture { get; set; }
}
