using Milvasoft.Components.CQRS.Command;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Dtos.WingetDtos;

namespace Storeasy.Application.Features.Winget.PlanWingetChange;

/// <summary>
/// Plans a winget pull request: a new version (manifests built from the latest version's, with new installers given here
/// or taken from the app's Microsoft Store MSI/EXE product) or locale text edits of an existing version. The manifests are
/// checked against the schema and the winget-pkgs policies first; the plan shows every file it pushes. Approving it pushes
/// a branch to the fork and opens the pull request to microsoft/winget-pkgs; the merge (by the winget moderators) publishes
/// the version. Dry run by default.
/// </summary>
[OrgAuth(OrgPermission.EditContent)]
public record PlanWingetChangeCommand : ICommand<ChangePlanDto>, IOrganizationRequest
{
    /// <inheritdoc/>
    public int OrganizationId { get; set; }

    /// <summary>
    /// winget store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// New version or locale texts.
    /// </summary>
    public WingetChangeKind Kind { get; set; } = WingetChangeKind.NewVersion;

    /// <summary>
    /// The new version (required for a new version), or the version whose texts change (null: the latest).
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Installers of a new version.
    /// </summary>
    public List<WingetInstallerInputDto> Installers { get; set; } = [];

    /// <summary>
    /// Also take the installers of the app's Microsoft Store MSI/EXE product (one entry per architecture).
    /// </summary>
    public bool FromMicrosoftStore { get; set; }

    /// <summary>
    /// Locale text edits (release notes of a new version go here too). A new package needs its default locale's
    /// Publisher, PackageName, License and ShortDescription.
    /// </summary>
    public List<WingetLocaleInputDto> Locales { get; set; } = [];

    /// <summary>
    /// True (default): only save the plan for approval.
    /// </summary>
    public bool DryRun { get; set; } = true;

    /// <summary>
    /// Confirmation text when the plan is approved at once (the package identifier and the version).
    /// </summary>
    public string Confirm { get; set; }
}
