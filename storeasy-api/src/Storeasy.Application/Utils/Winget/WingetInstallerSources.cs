using Storeasy.Application.Dtos.WingetDtos;
using Storeasy.Domain.MsStore;

namespace Storeasy.Application.Utils.Winget;

/// <summary>
/// What the Microsoft Store MSI/EXE engine already knows about an installer (address, type, architectures, languages,
/// silent switches) in winget terms, so a new winget version can start from the same packages.
/// </summary>
public static class WingetInstallerSources
{
    /// <summary>
    /// The installer of a Microsoft Store MSI/EXE package as a winget installer source.
    /// </summary>
    /// <param name="package"></param>
    /// <returns></returns>
    public static WingetInstallerSourceDto From(MsInstallerPackage package) => new()
    {
        Url = package.PackageUrl,
        Type = package.PackageType == MsInstallerType.Msi ? "msi" : "exe",
        Architectures = [.. (package.Architectures ?? []).Select(Architecture).Where(a => a != null).Distinct()],
        SilentSwitches = package.PackageType == MsInstallerType.Exe ? package.InstallerParameters : null,
        Locales = package.Languages ?? [],
        SignatureStatus = package.SignatureStatus,
    };

    /// <summary>
    /// The installers of a new version taken from the sources: one entry per architecture. A source without an
    /// architecture counts as x64 (Partner Center requires one, so this only happens for incomplete rows).
    /// </summary>
    /// <param name="sources"></param>
    /// <returns></returns>
    public static List<WingetInstallerInputDto> ToInputs(IEnumerable<WingetInstallerSourceDto> sources)
        => [.. sources.SelectMany(s => (s.Architectures.Count == 0 ? ["x64"] : s.Architectures).Select(a => new WingetInstallerInputDto
        {
            Url = s.Url,
            Architecture = a,
            Type = s.Type,
            SilentSwitches = s.SilentSwitches,
            Locale = s.Locales.Count == 1 ? s.Locales[0] : null,
        }))];

    /// <summary>
    /// winget architecture of a Partner Center one (Neutral, X86, X64, Arm, Arm64); null for an unknown value.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static string Architecture(string value) => value?.Trim().ToLowerInvariant() switch
    {
        "neutral" => "neutral",
        "x86" => "x86",
        "x64" => "x64",
        "arm" => "arm",
        "arm64" => "arm64",
        _ => null,
    };
}
