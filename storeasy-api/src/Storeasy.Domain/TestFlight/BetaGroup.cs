using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.TestFlight;

/// <summary>
/// A TestFlight beta group as last read from App Store Connect (the TestFlight sync replaces the rows of a store record).
/// </summary>
[Table(TableNames.BetaGroups)]
[Index(nameof(StoreAppId), nameof(ExternalId), IsUnique = true)]
[Index(nameof(OrganizationId))]
public class BetaGroup : BaseEntity<int>
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
    /// The store's id of the group.
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string ExternalId { get; set; }

    /// <summary>
    /// Name.
    /// </summary>
    [MaxLength(TextMaxLength)]
    public string Name { get; set; }

    /// <summary>
    /// Internal group (App Store Connect users) or external.
    /// </summary>
    public bool IsInternal { get; set; }

    /// <summary>
    /// Whether the public link is on.
    /// </summary>
    public bool PublicLinkEnabled { get; set; }

    /// <summary>
    /// Public link.
    /// </summary>
    [MaxLength(500)]
    public string PublicLink { get; set; }

    /// <summary>
    /// Most testers the public link admits.
    /// </summary>
    public int? PublicLinkLimit { get; set; }

    /// <summary>
    /// Whether the limit is on.
    /// </summary>
    public bool PublicLinkLimitEnabled { get; set; }

    /// <summary>
    /// Testers in the group.
    /// </summary>
    public int TesterCount { get; set; }

    /// <summary>
    /// The store's ids of the builds in the group.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<string> BuildExternalIds { get; set; } = [];

    /// <summary>
    /// UTC instant of the last sync.
    /// </summary>
    public DateTime SyncedDate { get; set; }
}
