using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Events;

/// <summary>
/// Last known review state of one store version (an App Store version, later a Microsoft submission), kept so a change is
/// noticed once whether it arrives by webhook or by the periodic sync, and so the submission history of an app can be
/// shown with accepted versions too.
/// </summary>
[Table(TableNames.AppVersionStates)]
[Index(nameof(StoreAppId), nameof(ExternalVersionId), IsUnique = true)]
[Index(nameof(OrganizationId))]
public class AppVersionState : BaseEntity<long>
{
    /// <summary>
    /// Column length of the short text columns.
    /// </summary>
    public const int TextMaxLength = 100;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// storeasy app.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// The store's id of the version.
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string ExternalVersionId { get; set; }

    /// <summary>
    /// User facing version (1.4).
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string VersionString { get; set; }

    /// <summary>
    /// Build number attached to the version, if known.
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string BuildNumber { get; set; }

    /// <summary>
    /// State as the store names it (e.g. <c>WAITING_FOR_REVIEW</c>, <c>REJECTED</c>).
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string State { get; set; }

    /// <summary>
    /// Previous state.
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string PreviousState { get; set; }

    /// <summary>
    /// UTC instant of the last change.
    /// </summary>
    public DateTime ChangedDate { get; set; }

    /// <summary>
    /// UTC instant the version was first seen in a submitted state.
    /// </summary>
    public DateTime? SubmittedDate { get; set; }
}
