using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;
using Storeasy.Infrastructure.Storage;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Storeasy.IntegrationTests.ServicesTests;

/// <summary>
/// The real <see cref="S3FileStorage"/> against a SeaweedFS container (its S3 gateway, the image docker-compose.yml uses):
/// presigned single and multipart uploads, metadata, presigned downloads and deletes. The S3 identity is a config file
/// copied into the container before it starts.
/// Every HttpClient of the process is proxied to a closed port by the API tests, so the clients here skip the proxy and
/// the container is awaited by its log line, not by an HTTP probe (which would go through that proxy).
/// </summary>
[Trait("Service Integration Tests", "S3 file storage against SeaweedFS.")]
public sealed class S3FileStorageTests : IAsyncLifetime
{
    private const string _bucket = "storeasy-tests";
    private const string _user = "storeasy-tests";
    private const string _password = "storeasy-tests-password";
    private const ushort _port = 8333;
    private const string _configPath = "/etc/seaweedfs/s3.json";

    private static readonly string _s3Config = $$"""
                                                {"identities":[{"name":"tests","credentials":[{"accessKey":"{{_user}}","secretKey":"{{_password}}"}],"actions":["Admin","Read","Write","List","Tagging"]}]}
                                                """;

    private readonly IContainer _seaweedFs = new ContainerBuilder("chrislusf/seaweedfs:4.53")
                                                 .WithResourceMapping(System.Text.Encoding.UTF8.GetBytes(_s3Config), _configPath)
                                                 .WithCommand("server", "-s3", $"-s3.config={_configPath}", "-dir=/data")
                                                 .WithPortBinding(_port, true)
                                                 .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Start Seaweed S3 API Server"))
                                                 .WithCleanUp(true)
                                                 .Build();

    private readonly HttpClient _http = new(new HttpClientHandler { UseProxy = false });

    private IAmazonS3 _client;
    private S3FileStorage _storage;

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        await _seaweedFs.StartAsync();

        var options = new StorageOptions
        {
            Bucket = _bucket,
            Region = "eu-central-1",
            AccessKey = _user,
            SecretKey = _password,
            ServiceUrl = $"http://{_seaweedFs.Hostname}:{_seaweedFs.GetMappedPublicPort(_port)}",
            ForcePathStyle = true,
        };

        _client = S3FileStorage.CreateClient(options, new NoProxyHttpClientFactory());
        _storage = new S3FileStorage(_client, options);

        await _client.PutBucketAsync(new PutBucketRequest { BucketName = _bucket });
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        _http.Dispose();

        await _seaweedFs.DisposeAsync();
    }

    [Fact]
    public async Task SingleUpload_ShouldStoreTheObjectAndServeItBack()
    {
        // Arrange
        const string key = "org/1/screenshot/a/home.png";
        var content = RandomNumberGenerator.GetBytes(4096);

        // Act
        var put = await PutAsync(_storage.CreateUploadUrl(key, "image/png", TimeSpan.FromMinutes(5)), content, "image/png");
        var info = await _storage.GetObjectInfoAsync(key, TestContext.Current.CancellationToken);
        var downloaded = await _http.GetByteArrayAsync(_storage.CreateDownloadUrl(key, TimeSpan.FromMinutes(5), "home.png"), TestContext.Current.CancellationToken);

        // Assert
        put.IsSuccessStatusCode.Should().BeTrue(await put.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        info.Size.Should().Be(content.Length);
        info.ContentType.Should().Be("image/png");
        downloaded.Should().Equal(content);
    }

    [Fact]
    public async Task MultipartUpload_ShouldJoinThePartsInOrder()
    {
        // Arrange: every part but the last must be at least 5 MiB.
        const string key = "org/1/buildartifact/b/app.aab";
        var first = RandomNumberGenerator.GetBytes(5 * 1024 * 1024);
        var second = RandomNumberGenerator.GetBytes(1000);

        // Act
        var uploadId = await _storage.StartMultipartUploadAsync(key, "application/octet-stream", TestContext.Current.CancellationToken);
        var firstPut = await PutAsync(_storage.CreatePartUploadUrl(key, uploadId, 1, TimeSpan.FromMinutes(5)), first);
        var secondPut = await PutAsync(_storage.CreatePartUploadUrl(key, uploadId, 2, TimeSpan.FromMinutes(5)), second);

        await _storage.CompleteMultipartUploadAsync(key, uploadId, [new UploadedPart(2, secondPut.Headers.ETag.Tag), new UploadedPart(1, firstPut.Headers.ETag.Tag)], TestContext.Current.CancellationToken);

        var info = await _storage.GetObjectInfoAsync(key, TestContext.Current.CancellationToken);
        var downloaded = await _http.GetByteArrayAsync(_storage.CreateDownloadUrl(key, TimeSpan.FromMinutes(5)), TestContext.Current.CancellationToken);

        // Assert
        info.Size.Should().Be(first.Length + second.Length);
        downloaded.Should().Equal([.. first, .. second]);
    }

    [Fact]
    public async Task MissingAndDeletedObjects_ShouldHaveNoInfo()
    {
        const string key = "org/1/graphic/c/icon.png";

        var missing = await _storage.GetObjectInfoAsync(key, TestContext.Current.CancellationToken);

        await PutAsync(_storage.CreateUploadUrl(key, "image/png", TimeSpan.FromMinutes(5)), [1, 2, 3], "image/png");
        await _storage.DeleteAsync(key, TestContext.Current.CancellationToken);

        var deleted = await _storage.GetObjectInfoAsync(key, TestContext.Current.CancellationToken);

        missing.Should().BeNull();
        deleted.Should().BeNull();
    }

    private async Task<HttpResponseMessage> PutAsync(string url, byte[] content, string contentType = null)
    {
        using var body = new ByteArrayContent(content);

        if (contentType != null)
            body.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        return await _http.PutAsync(url, body, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The SDK's HTTP clients without the process wide proxy of the test run.
    /// </summary>
    private sealed class NoProxyHttpClientFactory : HttpClientFactory
    {
        public override HttpClient CreateHttpClient(IClientConfig clientConfig) => new(new HttpClientHandler { UseProxy = false });
    }
}
