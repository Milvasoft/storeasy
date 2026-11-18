namespace Storeasy.Application.Utils.Models;

/// <summary>
/// S3 compatible object storage settings (configuration section <see cref="SectionName"/>, docs/urun-plani.md section 10),
/// bound once and registered as a singleton. The bucket is private; every read and write goes through presigned URLs.
/// The keys come only from user secrets or the environment (<c>Storage__S3__AccessKey</c>, <c>Storage__S3__SecretKey</c>).
/// </summary>
public class StorageOptions
{
    /// <summary>
    /// Configuration section of the options.
    /// </summary>
    public const string SectionName = "Storage:S3";

    /// <summary>
    /// Bucket name. Empty disables the storage (the upload endpoints answer 503).
    /// </summary>
    public string Bucket { get; set; }

    /// <summary>
    /// AWS region of the bucket (also the signing region for MinIO).
    /// </summary>
    public string Region { get; set; } = "eu-central-1";

    /// <summary>
    /// Access key id (secret, set through the environment).
    /// </summary>
    public string AccessKey { get; set; }

    /// <summary>
    /// Secret access key (secret, set through the environment).
    /// </summary>
    public string SecretKey { get; set; }

    /// <summary>
    /// Endpoint of an S3 compatible server (MinIO, e.g. <c>http://localhost:9000</c>). Empty for AWS S3.
    /// </summary>
    public string ServiceUrl { get; set; }

    /// <summary>
    /// Path style addressing (<c>host/bucket/key</c>), needed by MinIO. Ignored without <see cref="ServiceUrl"/>.
    /// </summary>
    public bool ForcePathStyle { get; set; } = true;

    /// <summary>
    /// Lifetime of presigned upload and download URLs, in minutes.
    /// </summary>
    public int PresignedUrlLifetimeMinutes { get; set; } = 30;

    /// <summary>
    /// Whether the storage can be used.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Bucket);
}
