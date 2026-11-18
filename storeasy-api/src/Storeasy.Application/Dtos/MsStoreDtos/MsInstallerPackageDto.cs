using Milvasoft.Attributes.Annotations;
using Storeasy.Domain.MsStore;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.MsStoreDtos;

/// <summary>
/// An MSI/EXE installer package with the result of storeasy's Authenticode check.
/// </summary>
public class MsInstallerPackageDto
{
    /// <summary>
    /// Row id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Store's package id.
    /// </summary>
    public string PackageId { get; set; }

    /// <summary>
    /// Installer address.
    /// </summary>
    public string PackageUrl { get; set; }

    /// <summary>
    /// Installer type.
    /// </summary>
    public MsInstallerType PackageType { get; set; }

    /// <summary>
    /// Architectures.
    /// </summary>
    public List<string> Architectures { get; set; }

    /// <summary>
    /// Languages.
    /// </summary>
    public List<string> Languages { get; set; }

    /// <summary>
    /// Silent install switches.
    /// </summary>
    public string InstallerParameters { get; set; }

    /// <summary>
    /// Whether the installer runs silently without parameters.
    /// </summary>
    public bool IsSilentInstall { get; set; }

    /// <summary>
    /// Return code documentation.
    /// </summary>
    public string GenericDocUrl { get; set; }

    /// <summary>
    /// Authenticode check result.
    /// </summary>
    public MsSignatureStatus SignatureStatus { get; set; }

    /// <summary>
    /// Note of the check.
    /// </summary>
    public string SignatureNote { get; set; }

    /// <summary>
    /// UTC instant of the check.
    /// </summary>
    public DateTime? CheckedDate { get; set; }

    /// <summary>
    /// Projection from a package row.
    /// </summary>
    [JsonIgnore]
    [ExcludeFromMetadata]
    public static Expression<Func<MsInstallerPackage, MsInstallerPackageDto>> Projection { get; } = p => new MsInstallerPackageDto
    {
        Id = p.Id,
        StoreAppId = p.StoreAppId,
        PackageId = p.PackageId,
        PackageUrl = p.PackageUrl,
        PackageType = p.PackageType,
        Architectures = p.Architectures,
        Languages = p.Languages,
        InstallerParameters = p.InstallerParameters,
        IsSilentInstall = p.IsSilentInstall,
        GenericDocUrl = p.GenericDocUrl,
        SignatureStatus = p.SignatureStatus,
        SignatureNote = p.SignatureNote,
        CheckedDate = p.CheckedDate,
    };
}
