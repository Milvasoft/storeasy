using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.TestFlight;

/// <summary>
/// A TestFlight tester of an app as last read from App Store Connect.
/// </summary>
[Table(TableNames.BetaTesters)]
[Index(nameof(StoreAppId), nameof(ExternalId), IsUnique = true)]
[Index(nameof(OrganizationId))]
public class BetaTester : BaseEntity<long>
{
    /// <summary>
    /// Column length of the short text columns.
    /// </summary>
    public const int TextMaxLength = 200;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Store record (App Store).
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// The store's id of the tester.
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string ExternalId { get; set; }

    /// <summary>
    /// First name.
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string FirstName { get; set; }

    /// <summary>
    /// Last name.
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string LastName { get; set; }

    /// <summary>
    /// E-mail (null for anonymous public link testers).
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string Email { get; set; }

    /// <summary>
    /// How the tester joined.
    /// </summary>
    public BetaInviteType InviteType { get; set; }

    /// <summary>
    /// State.
    /// </summary>
    public BetaTesterState State { get; set; }

    /// <summary>
    /// The store's ids of the groups the tester is in, as <c>,id1,id2,</c> (searchable with a plain LIKE).
    /// </summary>
    [MaxLength(4000)]
    public string GroupKeys { get; set; }

    /// <summary>
    /// UTC instant of the last sync.
    /// </summary>
    public DateTime SyncedDate { get; set; }
}
