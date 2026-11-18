using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;
using Storeasy.Infrastructure.Stores;
using Storeasy.Infrastructure.Stores.Content;
using System.Security.Cryptography;
using System.Text.Json;

namespace Storeasy.Infrastructure.Intake;

/// <summary>
/// Uploads an IPA with the App Store Connect build upload resources (checked against Apple's documentation on
/// 18 November 2026): <c>POST /v1/buildUploads</c> (cfBundleShortVersionString, cfBundleVersion, platform, app),
/// <c>POST /v1/buildUploadFiles</c> (assetType ASSET, fileName, fileSize, uti com.apple.ipa) whose response carries the
/// presigned <c>uploadOperations</c>, a PUT of each byte range streamed from the bucket, then
/// <c>PATCH /v1/buildUploadFiles/{id}</c> with <c>uploaded: true</c> and the file's MD5 in <c>sourceFileChecksums</c>. The
/// upload's state (AWAITING_UPLOAD, PROCESSING, FAILED, COMPLETE) and its build come from <c>GET /v1/buildUploads/{id}</c>.
/// End to end this path still needs one real build to confirm it (plan 11.8); <see cref="ExternalAppleBuildUploader"/> is
/// the fallback.
/// </summary>
/// <param name="http"></param>
/// <param name="storage"></param>
public sealed class AppleBuildUploader(StoreHttp http, IArtifactStorage storage) : IAppleBuildUploader
{
    private const int _maxOperationBytes = 256 * 1024 * 1024;

    private readonly StoreHttp _http = http;
    private readonly IArtifactStorage _storage = storage;

    /// <inheritdoc/>
    public bool Uploads => true;

    /// <inheritdoc/>
    public async Task<string> UploadAsync(AppleBuildUploadRequest request, IStoreJobContext context, CancellationToken cancellationToken)
    {
        var api = Api(request.SecretJson);

        var uploadId = await ContentSteps.RunAsync(context, "build.upload.create", $"{request.Package.VersionName} ({request.Package.VersionCode})", async () =>
        {
            using var created = await api.PostAsync("/v1/buildUploads", new
            {
                data = new
                {
                    type = "buildUploads",
                    attributes = new
                    {
                        cfBundleShortVersionString = request.Package.VersionName,
                        cfBundleVersion = request.Package.VersionCode,
                        platform = request.Package.ApplePlatform ?? "IOS",
                    },
                    relationships = new { app = new { data = new { type = "apps", id = request.AppleAppId } } },
                },
            }, cancellationToken);

            return JsonRead.Str(created.RootElement.GetProperty("data"), "id");
        }, cancellationToken);

        using var file = await api.PostAsync("/v1/buildUploadFiles", new
        {
            data = new
            {
                type = "buildUploadFiles",
                attributes = new { assetType = "ASSET", fileName = request.FileName, fileSize = request.Size, uti = "com.apple.ipa" },
                relationships = new { buildUpload = new { data = new { type = "buildUploads", id = uploadId } } },
            },
        }, cancellationToken);

        var reservation = file.RootElement.GetProperty("data").Clone();
        var fileId = JsonRead.Str(reservation, "id");
        var operations = JsonRead.Items(JsonRead.Get(reservation, "attributes"), "uploadOperations")
                                 .Select(o => new
                                 {
                                     Offset = Long(o, "offset"),
                                     Length = Long(o, "length"),
                                     Method = JsonRead.Str(o, "method") ?? "PUT",
                                     Url = JsonRead.Str(o, "url"),
                                     Headers = JsonRead.Items(o, "requestHeaders").Select(h => (Name: JsonRead.Str(h, "name"), Value: JsonRead.Str(h, "value"))).ToList(),
                                 })
                                 .OrderBy(o => o.Offset)
                                 .ToList();

        if (operations.Count == 0 || operations.Sum(o => o.Length) != request.Size || operations.Any(o => o.Length > _maxOperationBytes))
            throw new StoreCallException("Apple returned upload operations that do not cover the file.", null, false);

        var step = await context.BeginStepAsync("build.upload.file", StepDirection.Outbound, request.FileName, request.Size, cancellationToken);
        using var md5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        long sent = 0;

        foreach (var operation in operations)
        {
            var bytes = await _storage.ReadRangeAsync(request.ObjectKey, operation.Offset, (int)operation.Length, cancellationToken);

            md5.AppendData(bytes);

            await _http.SendAsync(() =>
            {
                var content = new ByteArrayContent(bytes);
                var message = new HttpRequestMessage(new HttpMethod(operation.Method), operation.Url) { Content = content };

                foreach (var (name, value) in operation.Headers)
                {
                    if (!message.Headers.TryAddWithoutValidation(name, value))
                        content.Headers.TryAddWithoutValidation(name, value);
                }

                return message;
            }, true, cancellationToken);

            sent += bytes.Length;

            await context.ReportProgressAsync(step, sent, cancellationToken);
        }

        await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, $"{operations.Count} parts", cancellationToken);

        var checksum = Convert.ToHexStringLower(md5.GetHashAndReset());

        await ContentSteps.RunAsync(context, "build.upload.commit", fileId, async () =>
        {
            using var _ = await api.PatchAsync($"/v1/buildUploadFiles/{fileId}", new
            {
                data = new
                {
                    type = "buildUploadFiles",
                    id = fileId,
                    attributes = new { uploaded = true, sourceFileChecksums = new { file = new { algorithm = "MD5", hash = checksum } } },
                },
            }, cancellationToken);

            return "uploaded";
        }, cancellationToken);

        return uploadId;
    }

    /// <inheritdoc/>
    public async Task<AppleBuildUploadState> GetStateAsync(string secretJson, string buildUploadId, CancellationToken cancellationToken)
    {
        using var document = await Api(secretJson).GetOrNullAsync($"/v1/buildUploads/{buildUploadId}?include=build", cancellationToken);

        if (document == null)
            return new AppleBuildUploadState(null, null, null);

        var data = document.RootElement.GetProperty("data");
        var state = JsonRead.Get(JsonRead.Get(data, "attributes"), "state");
        var buildId = JsonRead.Str(JsonRead.Get(JsonRead.Get(JsonRead.Get(data, "relationships"), "build"), "data"), "id");
        var errors = string.Join("; ", JsonRead.Items(state, "errors").Select(e => JsonRead.Str(e, "description") ?? JsonRead.Str(e, "code")).Where(e => e != null));

        return new AppleBuildUploadState(JsonRead.Str(state, "state"), buildId, errors.Length == 0 ? null : errors);
    }

    private AppStoreConnectApi Api(string secretJson) => AppleTestFlightClient.CreateApi(_http, secretJson);

    private static long Long(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number) ? number : 0L;
}

/// <summary>
/// The documented fallback of the Apple delivery (plan 11.8): the CI uploads the build to Apple itself (EAS Submit, Xcode
/// Cloud, Transporter); storeasy uploads nothing and the delivery waits for the build with the package's version and build
/// number to appear in App Store Connect, then applies the delivery rule. Selected with
/// <c>Storeasy:Intake:AppleUploadMode = External</c>.
/// </summary>
public sealed class ExternalAppleBuildUploader : IAppleBuildUploader
{
    /// <inheritdoc/>
    public bool Uploads => false;

    /// <inheritdoc/>
    public Task<string> UploadAsync(AppleBuildUploadRequest request, IStoreJobContext context, CancellationToken cancellationToken) => Task.FromResult<string>(null);

    /// <inheritdoc/>
    public Task<AppleBuildUploadState> GetStateAsync(string secretJson, string buildUploadId, CancellationToken cancellationToken)
        => Task.FromResult(new AppleBuildUploadState(null, null, null));
}

/// <summary>
/// App Store Connect reads and writes the Apple delivery needs around the upload: the build of a version and build number,
/// its processing state and export compliance. TestFlight groups and "What to Test" go through the phase 4
/// <see cref="ITestFlightGateway"/>.
/// </summary>
/// <param name="http"></param>
public sealed class AppleTestFlightClient(StoreHttp http)
{
    private readonly StoreHttp _http = http;

    /// <summary>
    /// The build of <paramref name="versionName"/> (<paramref name="buildNumber"/>) of an app: id, processing state and
    /// export compliance answer; null when App Store Connect has none yet.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="appleAppId"></param>
    /// <param name="versionName"></param>
    /// <param name="buildNumber"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<(string Id, string ProcessingState, bool? UsesNonExemptEncryption)?> FindBuildAsync(string secretJson, string appleAppId, string versionName, string buildNumber, CancellationToken cancellationToken)
    {
        var path = $"/v1/builds?filter[app]={Uri.EscapeDataString(appleAppId)}&filter[version]={Uri.EscapeDataString(buildNumber)}"
                   + $"&filter[preReleaseVersion.version]={Uri.EscapeDataString(versionName ?? string.Empty)}&fields[builds]=version,processingState,usesNonExemptEncryption&limit=5";

        using var document = await Api(secretJson).GetAsync(path, cancellationToken);

        var build = JsonRead.Items(document.RootElement, "data").FirstOrDefault();

        if (build.ValueKind != JsonValueKind.Object)
            return null;

        var attributes = JsonRead.Get(build, "attributes");
        var encryption = JsonRead.Get(attributes, "usesNonExemptEncryption");

        return (JsonRead.Str(build, "id"), JsonRead.Str(attributes, "processingState"), encryption.ValueKind is JsonValueKind.True or JsonValueKind.False ? encryption.GetBoolean() : null);
    }

    /// <summary>
    /// Reads a build by id: processing state and export compliance answer.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="buildId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<(string ProcessingState, bool? UsesNonExemptEncryption)> GetBuildAsync(string secretJson, string buildId, CancellationToken cancellationToken)
    {
        using var document = await Api(secretJson).GetAsync($"/v1/builds/{buildId}?fields[builds]=processingState,usesNonExemptEncryption", cancellationToken);

        var attributes = JsonRead.Get(document.RootElement.GetProperty("data"), "attributes");
        var encryption = JsonRead.Get(attributes, "usesNonExemptEncryption");

        return (JsonRead.Str(attributes, "processingState"), encryption.ValueKind is JsonValueKind.True or JsonValueKind.False ? encryption.GetBoolean() : null);
    }

    /// <summary>
    /// Answers export compliance on a build (the IPA's ITSAppUsesNonExemptEncryption).
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="buildId"></param>
    /// <param name="usesNonExemptEncryption"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task SetExportComplianceAsync(string secretJson, string buildId, bool usesNonExemptEncryption, CancellationToken cancellationToken)
    {
        using var _ = await Api(secretJson).PatchAsync($"/v1/builds/{buildId}", new
        {
            data = new { type = "builds", id = buildId, attributes = new { usesNonExemptEncryption } },
        }, cancellationToken);
    }

    /// <summary>
    /// A JSON:API client for the key in <paramref name="secretJson"/>.
    /// </summary>
    /// <param name="http"></param>
    /// <param name="secretJson"></param>
    /// <returns></returns>
    public static AppStoreConnectApi CreateApi(StoreHttp http, string secretJson)
    {
        var credentials = StoreCredentialParser.ParseApple(secretJson, out _) ?? throw new StoreCallException("The App Store Connect key of the connection is not valid.", null, false);

        return new AppStoreConnectApi(http, credentials);
    }

    private AppStoreConnectApi Api(string secretJson) => CreateApi(_http, secretJson);
}
