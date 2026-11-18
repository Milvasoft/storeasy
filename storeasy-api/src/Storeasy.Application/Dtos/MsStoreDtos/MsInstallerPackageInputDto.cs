namespace Storeasy.Application.Dtos.MsStoreDtos;

/// <summary>
/// An MSI/EXE installer package as the user sets it.
/// </summary>
public class MsInstallerPackageInputDto
{
    /// <summary>
    /// HTTPS address of the installer; a new version gets a new URL.
    /// </summary>
    public string PackageUrl { get; set; }

    /// <summary>
    /// Installer type.
    /// </summary>
    public MsInstallerType PackageType { get; set; }

    /// <summary>
    /// Neutral, X86, X64, Arm or Arm64.
    /// </summary>
    public string Architecture { get; set; }

    /// <summary>
    /// Languages of the installer.
    /// </summary>
    public List<string> Languages { get; set; } = [];

    /// <summary>
    /// Whether the installer runs silently without parameters.
    /// </summary>
    public bool IsSilentInstall { get; set; }

    /// <summary>
    /// Silent install switches (required when not silent).
    /// </summary>
    public string InstallerParameters { get; set; }

    /// <summary>
    /// Return code documentation (required for EXE).
    /// </summary>
    public string GenericDocUrl { get; set; }
}
