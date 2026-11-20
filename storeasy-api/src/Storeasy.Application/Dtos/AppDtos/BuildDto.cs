using Milvasoft.Attributes.Annotations;
using Storeasy.Domain.Apps;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace Storeasy.Application.Dtos.AppDtos;

/// <summary>
/// A build of one of the app's store records.
/// </summary>
public class BuildDto
{
    /// <summary>
    /// Build id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Store record.
    /// </summary>
    public int StoreAppId { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// The store's id of the build.
    /// </summary>
    public string ExternalId { get; set; }

    /// <summary>
    /// User facing version.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Build number or version code.
    /// </summary>
    public string BuildNumber { get; set; }

    /// <summary>
    /// Processing state.
    /// </summary>
    public BuildProcessingState ProcessingState { get; set; }

    /// <summary>
    /// Tracks the build is released to (Play).
    /// </summary>
    public string Tracks { get; set; }

    /// <summary>
    /// Minimum OS version.
    /// </summary>
    public string MinOsVersion { get; set; }

    /// <summary>
    /// UTC upload instant.
    /// </summary>
    public DateTime? UploadedDate { get; set; }

    /// <summary>
    /// UTC expiry instant (TestFlight).
    /// </summary>
    public DateTime? ExpiryDate { get; set; }

    /// <summary>
    /// Export compliance answer (App Store); null while unanswered.
    /// </summary>
    public bool? UsesNonExemptEncryption { get; set; }

    /// <summary>
    /// Upload session whose package storeasy still keeps for this build (download it with builds/download-url); null when
    /// the package was deleted or the build reached the store without storeasy.
    /// </summary>
    public long? PackageSessionId { get; set; }

    /// <summary>
    /// Projection from a build row.
    /// </summary>
    [JsonIgnore]
    [ExcludeFromMetadata]
    public static Expression<Func<Build, BuildDto>> Projection { get; } = b => new BuildDto
    {
        Id = b.Id,
        StoreAppId = b.StoreAppId,
        Store = b.Store,
        ExternalId = b.ExternalId,
        Version = b.Version,
        BuildNumber = b.BuildNumber,
        ProcessingState = b.ProcessingState,
        Tracks = b.Tracks,
        MinOsVersion = b.MinOsVersion,
        UploadedDate = b.UploadedDate,
        ExpiryDate = b.ExpiryDate,
        UsesNonExemptEncryption = b.UsesNonExemptEncryption,
    };
}
