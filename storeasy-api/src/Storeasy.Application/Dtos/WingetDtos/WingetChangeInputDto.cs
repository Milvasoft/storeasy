namespace Storeasy.Application.Dtos.WingetDtos;

/// <summary>
/// An installer of a new winget version.
/// </summary>
public class WingetInstallerInputDto
{
    /// <summary>
    /// Download address on the publisher's own site or release page (https, stable and specific to the version).
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Architecture of the installed binaries: <c>x86</c>, <c>x64</c>, <c>arm</c>, <c>arm64</c> or <c>neutral</c>.
    /// </summary>
    public string Architecture { get; set; }

    /// <summary>
    /// <c>msi</c>, <c>exe</c>, <c>msix</c>, <c>inno</c>, <c>nullsoft</c>, <c>wix</c>, <c>burn</c>, <c>zip</c>,
    /// <c>portable</c> or <c>appx</c>; empty keeps the previous version's type.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// <c>user</c> or <c>machine</c>; empty when the installer does not fix one.
    /// </summary>
    public string Scope { get; set; }

    /// <summary>
    /// Known SHA-256 of the file. Optional: storeasy downloads the file when the plan is applied and computes it; when
    /// given, the download must match or nothing is pushed.
    /// </summary>
    public string Sha256 { get; set; }

    /// <summary>
    /// Silent install switches (needed for <c>exe</c>).
    /// </summary>
    public string SilentSwitches { get; set; }

    /// <summary>
    /// Silent with progress switches.
    /// </summary>
    public string SilentWithProgressSwitches { get; set; }

    /// <summary>
    /// MSI product code (<c>{GUID}</c>), used by winget to match the installed program.
    /// </summary>
    public string ProductCode { get; set; }

    /// <summary>
    /// Locale of the installer, when it is language specific.
    /// </summary>
    public string Locale { get; set; }
}

/// <summary>
/// Text edits of one locale manifest.
/// </summary>
public class WingetLocaleInputDto
{
    /// <summary>
    /// Locale (<c>en-US</c>). The default locale's texts are required fields of the package.
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Field name (<c>PackageName</c>, <c>ShortDescription</c>, <c>Description</c>, <c>ReleaseNotes</c>, ...) to new
    /// value; an empty value removes an optional field.
    /// </summary>
    public Dictionary<string, string> Fields { get; set; } = [];

    /// <summary>
    /// New tags; null keeps the tags.
    /// </summary>
    public List<string> Tags { get; set; }
}
