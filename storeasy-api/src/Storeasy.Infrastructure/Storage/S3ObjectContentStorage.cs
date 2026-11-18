using Amazon.S3;
using Amazon.S3.Model;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;

namespace Storeasy.Infrastructure.Storage;

/// <summary>
/// <see cref="IObjectContentStorage"/> over AWSSDK.S3, on the same bucket and settings as <see cref="S3FileStorage"/>.
/// </summary>
/// <param name="client">The S3 client; null when the storage is not configured.</param>
/// <param name="options"></param>
public sealed class S3ObjectContentStorage(IAmazonS3 client, StorageOptions options) : IObjectContentStorage
{
    private readonly IAmazonS3 _client = client;
    private readonly StorageOptions _options = options;

    /// <inheritdoc/>
    public bool IsConfigured => _client != null && _options.IsConfigured;

    /// <inheritdoc/>
    public async Task<byte[]> ReadAsync(string key, CancellationToken cancellationToken)
    {
        using var response = await _client.GetObjectAsync(new GetObjectRequest { BucketName = _options.Bucket, Key = key }, cancellationToken);
        using var buffer = new MemoryStream();

        await response.ResponseStream.CopyToAsync(buffer, cancellationToken);

        return buffer.ToArray();
    }

    /// <inheritdoc/>
    public Task WriteAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
        => _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
        }, cancellationToken);

    /// <inheritdoc/>
    public Task DeleteAsync(string key, CancellationToken cancellationToken)
        => _client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = _options.Bucket, Key = key }, cancellationToken);
}
