using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.MsStore;

/// <summary>
/// An installer of an MSI/EXE product. storeasy does not host the file: the Store downloads it from
/// <see cref="PackageUrl"/> on the developer's server. The installer must be Authenticode signed; storeasy records the
/// result of its own check in <see cref="SignatureStatus"/> and refuses to submit while a package is not signed.
/// </summary>
[Table(TableNames.MsInstallerPackages)]
[Index(nameof(StoreAppId))]
[Index(nameof(OrganizationId))]
public class MsInstallerPackage : AuditableEntity<int>
{
    /// <summary>
    /// Column length of the URL columns.
    /// </summary>
    public const int UrlMaxLength = 2048;

    /// <summary>
    /// Column length of <see cref="InstallerParameters"/>.
    /// </summary>
    public const int InstallerParametersMaxLength = 1000;

    /// <summary>
    /// Column length of <see cref="SignatureNote"/>.
    /// </summary>
    public const int SignatureNoteMaxLength = 500;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// The store's package id. Null until the store saw the package.
    /// </summary>
    [MaxLength(MsProduct.IdMaxLength)]
    public string PackageId { get; set; }

    /// <summary>
    /// HTTPS address of the installer (versioned: a new version gets a new URL).
    /// </summary>
    [MaxLength(UrlMaxLength)]
    public string PackageUrl { get; set; }

    /// <summary>
    /// Installer type.
    /// </summary>
    public MsInstallerType PackageType { get; set; }

    /// <summary>
    /// Architectures (Neutral, X86, X64, Arm, Arm64).
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<string> Architectures { get; set; } = [];

    /// <summary>
    /// Languages of the installer.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<string> Languages { get; set; } = [];

    /// <summary>
    /// Silent install switches (required when <see cref="IsSilentInstall"/> is false).
    /// </summary>
    [MaxLength(InstallerParametersMaxLength)]
    public string InstallerParameters { get; set; }

    /// <summary>
    /// Whether the installer runs silently without parameters.
    /// </summary>
    public bool IsSilentInstall { get; set; }

    /// <summary>
    /// Documentation of the EXE installer's return codes (required for EXE).
    /// </summary>
    [MaxLength(UrlMaxLength)]
    public string GenericDocUrl { get; set; }

    /// <summary>
    /// Result of storeasy's Authenticode check.
    /// </summary>
    public MsSignatureStatus SignatureStatus { get; set; } = MsSignatureStatus.Unchecked;

    /// <summary>
    /// Short note of the check (signature kind found, bytes read, why it failed).
    /// </summary>
    [MaxLength(SignatureNoteMaxLength)]
    public string SignatureNote { get; set; }

    /// <summary>
    /// UTC instant of the last check.
    /// </summary>
    public DateTime? CheckedDate { get; set; }

    /// <summary>
    /// UTC instant of the last read from the store.
    /// </summary>
    public DateTime? SyncedDate { get; set; }

    /// <summary>
    /// Navigation property of the store record.
    /// </summary>
    [ForeignKey(nameof(StoreAppId))]
    public virtual StoreApp StoreApp { get; set; }
}
