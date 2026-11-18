using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Organizations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Intake;

/// <summary>
/// Package intake settings of an organization. An organization without a row uses the defaults.
/// </summary>
[Table(TableNames.IntakeSettings)]
[Index(nameof(OrganizationId), IsUnique = true)]
public class IntakeSetting : CreationAuditableEntity<int>
{
    /// <summary>
    /// Days a package is kept by default.
    /// </summary>
    public const int DefaultRetentionDays = 30;

    /// <summary>
    /// Largest package by default (4 GiB).
    /// </summary>
    public const long DefaultMaxArtifactBytes = 4L * 1024 * 1024 * 1024;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Days a stored package (and its session's file) is kept.
    /// </summary>
    public int ArtifactRetentionDays { get; set; } = DefaultRetentionDays;

    /// <summary>
    /// Largest package accepted, in bytes.
    /// </summary>
    public long MaxArtifactBytes { get; set; } = DefaultMaxArtifactBytes;

    /// <summary>
    /// Navigation property of the organization.
    /// </summary>
    public virtual Organization Organization { get; set; }
}
