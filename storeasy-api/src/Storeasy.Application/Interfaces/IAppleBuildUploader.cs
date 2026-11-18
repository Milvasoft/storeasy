using Storeasy.Domain.Intake;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// Sends an IPA to App Store Connect (docs/urun-plani.md 11.8 step 5). The default implementation uses the
/// <c>buildUploads</c> / <c>buildUploadFiles</c> resources (reserve, upload the operations, commit with the checksum); the
/// fallback implementation uploads nothing and lets the delivery wait for the build the CI uploaded itself.
/// </summary>
public interface IAppleBuildUploader
{
    /// <summary>
    /// Whether this implementation uploads (false: the CI uploads and storeasy only waits for the build).
    /// </summary>
    bool Uploads { get; }

    /// <summary>
    /// Uploads the package and returns the build upload id.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="context">Step reporting of the processing job.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<string> UploadAsync(AppleBuildUploadRequest request, IStoreJobContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Reads a build upload: its state and, once Apple made it, the build id. Null state when it is unknown.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="buildUploadId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<AppleBuildUploadState> GetStateAsync(string secretJson, string buildUploadId, CancellationToken cancellationToken);
}

/// <summary>
/// What to upload.
/// </summary>
/// <param name="SecretJson">Decrypted App Store Connect key; never logged.</param>
/// <param name="AppleAppId">App Store Connect app id.</param>
/// <param name="Package">What was read from the IPA.</param>
/// <param name="ObjectKey">Object key of the IPA.</param>
/// <param name="Size">Size in bytes.</param>
/// <param name="FileName">File name.</param>
public sealed record AppleBuildUploadRequest(string SecretJson, string AppleAppId, ArtifactPackageInfo Package, string ObjectKey, long Size, string FileName)
{
    /// <summary>
    /// Masks the secret so the record never prints it.
    /// </summary>
    /// <returns></returns>
    public override string ToString() => $"AppleBuildUploadRequest {{ AppleAppId = {this.AppleAppId}, FileName = {this.FileName} }}";
}

/// <summary>
/// State of a build upload.
/// </summary>
/// <param name="State">AWAITING_UPLOAD, PROCESSING, FAILED or COMPLETE.</param>
/// <param name="BuildId">Build id once known.</param>
/// <param name="Errors">Error descriptions of a failed upload.</param>
public sealed record AppleBuildUploadState(string State, string BuildId, string Errors);
