using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Organizations;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Intake;

/// <summary>
/// A CI token (<c>sci_</c>) for build services that cannot show an OIDC token (docs/urun-plani.md 11.8, design 13.8). It
/// belongs to the organization, not to a person, is limited to some apps (or all) and to the scopes in
/// <see cref="Scopes"/> (today only <see cref="ArtifactsWriteScope"/>), and always expires. Only the SHA-256 hash of the
/// value is stored.
/// </summary>
[Table(TableNames.CiTokens)]
[Index(nameof(TokenHash), IsUnique = true)]
[Index(nameof(OrganizationId))]
public class CiToken : CreationAuditableEntity<int>
{
    /// <summary>
    /// The only scope today: upload packages.
    /// </summary>
    public const string ArtifactsWriteScope = "artifacts:write";

    /// <summary>
    /// Column length of <see cref="Name"/>.
    /// </summary>
    public const int NameMaxLength = 60;

    /// <summary>
    /// Longest lifetime of a CI token.
    /// </summary>
    public const int MaxExpiryDays = 365;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Name (unique among the organization's active CI tokens).
    /// </summary>
    [MaxLength(NameMaxLength)]
    public string Name { get; set; }

    /// <summary>
    /// Lower case hex SHA-256 of the token value.
    /// </summary>
    [MaxLength(64)]
    public string TokenHash { get; set; }

    /// <summary>
    /// First characters of the value (<c>sci_7f2</c>).
    /// </summary>
    [MaxLength(7)]
    public string Prefix { get; set; }

    /// <summary>
    /// Whether the token may upload to every app of the organization.
    /// </summary>
    public bool AllApps { get; set; }

    /// <summary>
    /// Apps the token may upload to when <see cref="AllApps"/> is false.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<int> AppIds { get; set; } = [];

    /// <summary>
    /// Scopes.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<string> Scopes { get; set; } = [];

    /// <summary>
    /// UTC instant the token stops working.
    /// </summary>
    public DateTime ExpiryDate { get; set; }

    /// <summary>
    /// UTC instant of the last use.
    /// </summary>
    public DateTime? LastUseDate { get; set; }

    /// <summary>
    /// Client of the last use (User-Agent).
    /// </summary>
    [MaxLength(200)]
    public string LastUseSource { get; set; }

    /// <summary>
    /// UTC instant of revocation; null while active.
    /// </summary>
    public DateTime? RevokeDate { get; set; }

    /// <summary>
    /// Member who created the token.
    /// </summary>
    public int? CreatedByUserId { get; set; }

    /// <summary>
    /// Name of that member.
    /// </summary>
    [MaxLength(256)]
    public string CreatedByUserName { get; set; }

    /// <summary>
    /// Navigation property of the organization.
    /// </summary>
    public virtual Organization Organization { get; set; }
}
