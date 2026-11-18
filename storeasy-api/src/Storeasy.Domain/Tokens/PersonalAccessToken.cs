using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Organizations;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Tokens;

/// <summary>
/// A personal access token (<c>sea_</c>) for the REST API and the remote MCP (docs/urun-plani.md 2.6, 8). It belongs to
/// one user in one organization; its effective permission is the user's current role in that organization, capped to
/// view only when <see cref="ReadOnly"/>, and is computed again on every request. Only the SHA-256 hash of the value is
/// stored; the value is shown once, at creation.
/// </summary>
[Table(TableNames.PersonalAccessTokens)]
[Index(nameof(TokenHash), IsUnique = true)]
[Index(nameof(UserId), nameof(OrganizationId))]
public class PersonalAccessToken : CreationAuditableEntity<int>
{
    /// <summary>
    /// Column length of <see cref="Name"/>.
    /// </summary>
    public const int NameMaxLength = 60;

    /// <summary>
    /// Length of <see cref="Prefix"/> (the kind prefix and three characters).
    /// </summary>
    public const int PrefixLength = 7;

    /// <summary>
    /// Owner of the token.
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// Organization the token acts in.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// Name the owner gave the token (unique among the owner's active tokens of the organization).
    /// </summary>
    [MaxLength(NameMaxLength)]
    public string Name { get; set; }

    /// <summary>
    /// Lower case hex SHA-256 of the token value.
    /// </summary>
    [MaxLength(64)]
    public string TokenHash { get; set; }

    /// <summary>
    /// First characters of the value, shown in lists (<c>sea_9c1</c>).
    /// </summary>
    [MaxLength(PrefixLength)]
    public string Prefix { get; set; }

    /// <summary>
    /// Whether the token may only read.
    /// </summary>
    public bool ReadOnly { get; set; }

    /// <summary>
    /// UTC instant the token stops working.
    /// </summary>
    public DateTime ExpiryDate { get; set; }

    /// <summary>
    /// UTC instant of the last use (updated at most every few minutes).
    /// </summary>
    public DateTime? LastUseDate { get; set; }

    /// <summary>
    /// Client name of the last use (from the User-Agent header).
    /// </summary>
    [MaxLength(200)]
    public string LastClientName { get; set; }

    /// <summary>
    /// UTC instant of revocation; null while active.
    /// </summary>
    public DateTime? RevokeDate { get; set; }

    /// <summary>
    /// Navigation property of the owner.
    /// </summary>
    public virtual User User { get; set; }

    /// <summary>
    /// Navigation property of the organization.
    /// </summary>
    public virtual Organization Organization { get; set; }
}
