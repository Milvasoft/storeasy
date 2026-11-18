namespace Storeasy.Application.Utils.Models;

/// <summary>
/// Package intake settings (<c>Storeasy:Intake</c>).
/// </summary>
public class IntakeOptions
{
    /// <summary>
    /// Configuration section.
    /// </summary>
    public const string SectionName = "Storeasy:Intake";

    /// <summary>
    /// Apple delivery: <see cref="AppleUploadApi"/> uploads the IPA with the App Store Connect build upload resources;
    /// <see cref="AppleUploadExternal"/> is the documented fallback where the CI uploads to Apple itself (EAS Submit, Xcode
    /// Cloud, Transporter) and storeasy waits for the build in App Store Connect and applies the delivery rule.
    /// </summary>
    public string AppleUploadMode { get; set; } = AppleUploadApi;

    /// <summary>
    /// Apple upload mode: App Store Connect API build uploads.
    /// </summary>
    public const string AppleUploadApi = "Api";

    /// <summary>
    /// Apple upload mode: the CI uploads, storeasy waits for the build.
    /// </summary>
    public const string AppleUploadExternal = "External";

    /// <summary>
    /// Lifetime of the presigned part URLs (at least 60 minutes, tools/CONTRACT.md 3.2).
    /// </summary>
    public int UrlLifetimeMinutes { get; set; } = 120;

    /// <summary>
    /// Attempts of a processing job; an iOS delivery waits for App Store Connect processing by retrying with backoff.
    /// </summary>
    public int ProcessMaxAttempts { get; set; } = 40;

    /// <summary>
    /// Hours an upload session may stay open before its multipart upload is aborted.
    /// </summary>
    public int AbandonedUploadHours { get; set; } = 48;

    /// <summary>
    /// Hours a pending asset (phase 1 upload never confirmed) is kept before it and its object are removed.
    /// </summary>
    public int PendingAssetHours { get; set; } = 48;

    /// <summary>
    /// Most redirects followed when downloading a pulled package.
    /// </summary>
    public int MaxRedirects { get; set; } = 5;
}
