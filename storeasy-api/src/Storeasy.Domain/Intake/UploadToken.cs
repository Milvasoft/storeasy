using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Intake;

/// <summary>
/// A short lived upload token (<c>sut_</c>) issued by the OIDC exchange: one app, scope
/// <see cref="CiToken.ArtifactsWriteScope"/>, <see cref="Lifetime"/>. Only the SHA-256 hash is stored; expired rows are
/// deleted by the intake cleanup job.
/// </summary>
[Table(TableNames.UploadTokens)]
[Index(nameof(TokenHash), IsUnique = true)]
[Index(nameof(ExpiryDate))]
public class UploadToken : CreationAuditableEntity<long>
{
    /// <summary>
    /// How long an upload token works.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// The one app the token may upload to.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// Trusted publisher rule the OIDC token matched.
    /// </summary>
    public int TrustedPublisherId { get; set; }

    /// <summary>
    /// Lower case hex SHA-256 of the token value.
    /// </summary>
    [MaxLength(64)]
    public string TokenHash { get; set; }

    /// <summary>
    /// First characters of the value.
    /// </summary>
    [MaxLength(7)]
    public string Prefix { get; set; }

    /// <summary>
    /// UTC instant the token stops working.
    /// </summary>
    public DateTime ExpiryDate { get; set; }

    /// <summary>
    /// CI run that exchanged the token, if the claims named one.
    /// </summary>
    [MaxLength(500)]
    public string RunUrl { get; set; }
}
