using Amazon.S3;
using Amazon.S3.Model;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;
using System.Net;

namespace Storeasy.Infrastructure.Storage;

/// <summary>
/// <see cref="IArtifactStorage"/> on the same S3 compatible bucket as <see cref="S3FileStorage"/>.
/// </summary>
/// <param name="client"></param>
/// <param name="options"></param>
public sealed class S3ArtifactStorage(IAmazonS3 client, StorageOptions options) : IArtifactStorage
{
    private const int _maxListPages = 20;

    private readonly IAmazonS3 _client = client;
    private readonly StorageOptions _options = options;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StoredPart>> ListPartsAsync(string key, string uploadId, CancellationToken cancellationToken)
    {
        var parts = new List<StoredPart>();
        int? marker = null;

        try
        {
            for (var page = 0; page < _maxListPages; page++)
            {
                var response = await _client.ListPartsAsync(new ListPartsRequest
                {
                    BucketName = _options.Bucket,
                    Key = key,
                    UploadId = uploadId,
                    PartNumberMarker = marker?.ToString(),
                }, cancellationToken);

                parts.AddRange((response.Parts ?? []).Select(p => new StoredPart(p.PartNumber ?? 0, p.ETag, p.Size ?? 0)));

                if (response.IsTruncated != true || response.NextPartNumberMarker is not { } next)
                    break;

                marker = next;
            }
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        return [.. parts.OrderBy(p => p.PartNumber)];
    }

    /// <inheritdoc/>
    public async Task<string> UploadPartAsync(string key, string uploadId, int partNumber, Stream content, long length, CancellationToken cancellationToken)
    {
        var response = await _client.UploadPartAsync(new UploadPartRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            UploadId = uploadId,
            PartNumber = partNumber,
            InputStream = content,
            PartSize = length,
        }, cancellationToken);

        return response.ETag;
    }

    /// <inheritdoc/>
    public async Task<byte[]> ReadRangeAsync(string key, long offset, int length, CancellationToken cancellationToken)
    {
        if (length <= 0)
            return [];

        using var response = await _client.GetObjectAsync(new GetObjectRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            ByteRange = new ByteRange(offset, offset + length - 1),
        }, cancellationToken);

        using var buffer = new MemoryStream(length);

        await response.ResponseStream.CopyToAsync(buffer, cancellationToken);

        return buffer.ToArray();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<OpenMultipartUpload>> ListMultipartUploadsAsync(string prefix, DateTime startedBefore, CancellationToken cancellationToken)
    {
        var uploads = new List<OpenMultipartUpload>();
        string keyMarker = null;
        string uploadIdMarker = null;

        for (var page = 0; page < _maxListPages; page++)
        {
            var response = await _client.ListMultipartUploadsAsync(new ListMultipartUploadsRequest
            {
                BucketName = _options.Bucket,
                Prefix = prefix,
                KeyMarker = keyMarker,
                UploadIdMarker = uploadIdMarker,
            }, cancellationToken);

            uploads.AddRange((response.MultipartUploads ?? []).Where(u => (u.Initiated ?? DateTime.MaxValue).ToUniversalTime() < startedBefore)
                                                              .Select(u => new OpenMultipartUpload(u.Key, u.UploadId, (u.Initiated ?? DateTime.MaxValue).ToUniversalTime())));

            if (response.IsTruncated != true)
                break;

            keyMarker = response.NextKeyMarker;
            uploadIdMarker = response.NextUploadIdMarker;
        }

        return uploads;
    }
}
