using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.MsStore;

/// <summary>
/// Name of a Partner Center flight group. The API names flight groups only by id, so the name (and the member count, if
/// the user knows it) is entered in storeasy or recorded by the assisted step.
/// </summary>
[Table(TableNames.MsFlightGroups)]
[Index(nameof(OrganizationId), nameof(GroupId), IsUnique = true)]
public class MsFlightGroup : AuditableEntity<int>
{
    /// <summary>
    /// Column length of <see cref="Name"/>.
    /// </summary>
    public const int NameMaxLength = 100;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Partner Center flight group id.
    /// </summary>
    [MaxLength(MsProduct.IdMaxLength)]
    public string GroupId { get; set; }

    /// <summary>
    /// Group name.
    /// </summary>
    [MaxLength(NameMaxLength)]
    public string Name { get; set; }

    /// <summary>
    /// Number of members, when known.
    /// </summary>
    public int? MemberCount { get; set; }
}
