using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;
using System.Net;

namespace Storeasy.Infrastructure.Storage;

/// <summary>
/// <see cref="IFileStorage"/> over AWSSDK.S3 (AWS S3, or an S3 compatible server such as SeaweedFS with <see cref="StorageOptions.ServiceUrl"/> and path style
/// addressing). Presigned URLs use the scheme of the endpoint (HTTP for a local SeaweedFS).
/// </summary>
/// <param name="client">The S3 client; null when the storage is not configured.</param>
/// <param name="options"></param>
public sealed class S3FileStorage(IAmazonS3 client, StorageOptions options) : IFileStorage
{
    private readonly IAmazonS3 _client = client;
    private readonly StorageOptions _options = options;

    /// <inheritdoc/>
    public bool IsConfigured => _client != null && _options.IsConfigured;

    /// <summary>
    /// Builds the S3 client of <paramref name="options"/>, or null when no bucket is configured. Without an access key the
    /// SDK's default credential chain (instance role, environment) is used.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="httpClientFactory">Optional HTTP client factory of the SDK (tests bypass the process proxy with it).</param>
    /// <returns></returns>
    public static IAmazonS3 CreateClient(StorageOptions options, HttpClientFactory httpClientFactory = null)
    {
        if (!options.IsConfigured)
            return null;

        var config = new AmazonS3Config
        {
            // S3 compatible servers reject the checksums the SDK adds by default since v4.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };

        if (string.IsNullOrWhiteSpace(options.ServiceUrl))
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);
        }
        else
        {
            config.ServiceURL = options.ServiceUrl;
            config.AuthenticationRegion = options.Region;
            config.ForcePathStyle = options.ForcePathStyle;
        }

        if (httpClientFactory != null)
            config.HttpClientFactory = httpClientFactory;

        return string.IsNullOrWhiteSpace(options.AccessKey)
            ? new AmazonS3Client(config)
            : new AmazonS3Client(new BasicAWSCredentials(options.AccessKey, options.SecretKey), config);
    }

    /// <inheritdoc/>
    public string CreateUploadUrl(string key, string contentType, TimeSpan lifetime) => _client.GetPreSignedURL(new GetPreSignedUrlRequest
    {
        BucketName = _options.Bucket,
        Key = key,
        Verb = HttpVerb.PUT,
        ContentType = contentType,
        Expires = Expiry(lifetime),
        Protocol = Protocol(),
    });

    /// <inheritdoc/>
    public string CreateDownloadUrl(string key, TimeSpan lifetime, string downloadFileName = null)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = Expiry(lifetime),
            Protocol = Protocol(),
        };

        if (!string.IsNullOrWhiteSpace(downloadFileName))
            request.ResponseHeaderOverrides.ContentDisposition = $"attachment; filename=\"{downloadFileName.Replace("\"", string.Empty)}\"";

        return _client.GetPreSignedURL(request);
    }

    /// <inheritdoc/>
    public async Task<string> StartMultipartUploadAsync(string key, string contentType, CancellationToken cancellationToken)
    {
        var response = await _client.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            ContentType = contentType,
        }, cancellationToken);

        return response.UploadId;
    }

    /// <inheritdoc/>
    public string CreatePartUploadUrl(string key, string uploadId, int partNumber, TimeSpan lifetime) => _client.GetPreSignedURL(new GetPreSignedUrlRequest
    {
        BucketName = _options.Bucket,
        Key = key,
        Verb = HttpVerb.PUT,
        UploadId = uploadId,
        PartNumber = partNumber,
        Expires = Expiry(lifetime),
        Protocol = Protocol(),
    });

    /// <inheritdoc/>
    public Task CompleteMultipartUploadAsync(string key, string uploadId, IReadOnlyList<UploadedPart> parts, CancellationToken cancellationToken)
        => _client.CompleteMultipartUploadAsync(new CompleteMultipartUploadRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            UploadId = uploadId,
            PartETags = [.. parts.OrderBy(p => p.PartNumber).Select(p => new PartETag(p.PartNumber, p.ETag))],
        }, cancellationToken);

    /// <inheritdoc/>
    public Task AbortMultipartUploadAsync(string key, string uploadId, CancellationToken cancellationToken)
        => _client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest { BucketName = _options.Bucket, Key = key, UploadId = uploadId }, cancellationToken);

    /// <inheritdoc/>
    public async Task<StoredObjectInfo> GetObjectInfoAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var metadata = await _client.GetObjectMetadataAsync(new GetObjectMetadataRequest { BucketName = _options.Bucket, Key = key }, cancellationToken);

            return new StoredObjectInfo(metadata.ContentLength, metadata.Headers.ContentType);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string key, CancellationToken cancellationToken)
        => _client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = _options.Bucket, Key = key }, cancellationToken);

    /// <inheritdoc/>
    public async Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        var response = await _client.GetObjectAsync(new GetObjectRequest { BucketName = _options.Bucket, Key = key }, cancellationToken);

        return response.ResponseStream;
    }

    private static DateTime Expiry(TimeSpan lifetime) => DateTime.UtcNow.Add(lifetime);

    private Amazon.S3.Protocol Protocol()
        => !string.IsNullOrWhiteSpace(_options.ServiceUrl) && _options.ServiceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ? Amazon.S3.Protocol.HTTP : Amazon.S3.Protocol.HTTPS;
}
