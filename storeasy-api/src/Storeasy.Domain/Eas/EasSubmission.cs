using Microsoft.EntityFrameworkCore;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using Storeasy.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Storeasy.Domain.Eas;

/// <summary>
/// An EAS Submit submission storeasy started for a finished build: iOS to App Store Connect (TestFlight), Android to a
/// Play track.
/// </summary>
[Table(TableNames.EasSubmissions)]
[Index(nameof(OrganizationId), nameof(SubmissionId), IsUnique = true)]
[Index(nameof(AppId))]
[Index(nameof(Status))]
public class EasSubmission : AuditableEntity<long>
{
    /// <summary>
    /// Maximum length of the short text columns.
    /// </summary>
    public const int ShortMaxLength = 200;

    /// <summary>
    /// Owning organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// storeasy app.
    /// </summary>
    public int AppId { get; set; }

    /// <summary>
    /// storeasy id of the build.
    /// </summary>
    public long EasBuildId { get; set; }

    /// <summary>
    /// Expo submission id.
    /// </summary>
    [MaxLength(ShortMaxLength)]
    public string SubmissionId { get; set; }

    /// <summary>
    /// Platform.
    /// </summary>
    public EasPlatform Platform { get; set; }

    /// <summary>
    /// State.
    /// </summary>
    public EasSubmissionStatus Status { get; set; }

    /// <summary>
    /// Play track (Android); null for iOS (TestFlight).
    /// </summary>
    [MaxLength(ShortMaxLength)]
    public string Track { get; set; }

    /// <summary>
    /// Play release status (Android).
    /// </summary>
    [MaxLength(ShortMaxLength)]
    public string ReleaseStatus { get; set; }

    /// <summary>
    /// Who started it (a member, or the build's chain option).
    /// </summary>
    [MaxLength(ShortMaxLength)]
    public string StartedBy { get; set; }

    /// <summary>
    /// Whether the build's chain option started it.
    /// </summary>
    public bool FromChain { get; set; }

    /// <summary>
    /// Started.
    /// </summary>
    public DateTime? StartedDate { get; set; }

    /// <summary>
    /// Ended.
    /// </summary>
    public DateTime? FinishedDate { get; set; }

    /// <summary>
    /// First log file.
    /// </summary>
    [MaxLength(2000)]
    public string LogsUrl { get; set; }

    /// <summary>
    /// Expo's error code.
    /// </summary>
    [MaxLength(ShortMaxLength)]
    public string ErrorCode { get; set; }

    /// <summary>
    /// Expo's error message.
    /// </summary>
    [MaxLength(1000)]
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Whether the end was reported.
    /// </summary>
    public bool EndReported { get; set; }
}
