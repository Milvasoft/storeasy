namespace Storeasy.Application.Interfaces;

/// <summary>
/// The addons.mozilla.org API v5 (<c>/api/v5/</c>), signed with a JWT (HS256) made from the connection's issuer and secret
/// (AMO Developer Hub, Manage API Keys). Failures surface as <see cref="StoreCallException"/>.
/// </summary>
public interface IAmoClient
{
    /// <summary>
    /// <c>GET /accounts/profile/</c>: the account the key belongs to (the cheapest read that proves the key).
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<AmoProfileRead> GetProfileAsync(string secretJson, CancellationToken cancellationToken);

    /// <summary>
    /// <c>GET /addons/addon/</c>: the add-ons the account is an author of, every page.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IReadOnlyList<AmoAddonRead>> ListAddonsAsync(string secretJson, CancellationToken cancellationToken);

    /// <summary>
    /// <c>GET /addons/addon/{id|slug|guid}/</c> with every translation.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="addon">Numeric id, slug or guid.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<AmoAddonRead> GetAddonAsync(string secretJson, string addon, CancellationToken cancellationToken);

    /// <summary>
    /// <c>GET /addons/addon/{id}/versions/?filter=all_with_unlisted</c>: listed and unlisted versions, newest first, up to
    /// <paramref name="maxPages"/> pages of 50.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="addon"></param>
    /// <param name="maxPages"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IReadOnlyList<AmoVersionRead>> ListVersionsAsync(string secretJson, string addon, int maxPages, CancellationToken cancellationToken);

    /// <summary>
    /// <c>PATCH /addons/addon/{id}/</c> with a JSON body (listing texts per locale, links, categories).
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="addon"></param>
    /// <param name="patch">The fields to write.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<AmoAddonRead> UpdateAddonAsync(string secretJson, string addon, System.Text.Json.Nodes.JsonObject patch, CancellationToken cancellationToken);

    /// <summary>
    /// <c>PATCH /addons/addon/{id}/</c> with an <c>icon</c> file (multipart).
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="addon"></param>
    /// <param name="file"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task UploadIconAsync(string secretJson, string addon, AmoFile file, CancellationToken cancellationToken);

    /// <summary>
    /// <c>POST /addons/addon/{id}/previews/</c> with an <c>image</c> file (multipart).
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="addon"></param>
    /// <param name="file"></param>
    /// <param name="position">Sort position, or null for the end.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<AmoPreviewRead> AddPreviewAsync(string secretJson, string addon, AmoFile file, int? position, CancellationToken cancellationToken);

    /// <summary>
    /// <c>DELETE /addons/addon/{id}/previews/{previewId}/</c>.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="addon"></param>
    /// <param name="previewId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task DeletePreviewAsync(string secretJson, string addon, long previewId, CancellationToken cancellationToken);

    /// <summary>
    /// <c>POST /addons/upload/</c> with the package and its channel; AMO validates it in the background.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="file">The .xpi or .zip.</param>
    /// <param name="channel"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<AmoUploadRead> UploadAsync(string secretJson, AmoFile file, FirefoxVersionChannel channel, CancellationToken cancellationToken);

    /// <summary>
    /// <c>GET /addons/upload/{uuid}/</c>: whether the validation finished and passed.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="uuid"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<AmoUploadRead> GetUploadAsync(string secretJson, string uuid, CancellationToken cancellationToken);

    /// <summary>
    /// <c>POST /addons/addon/{id}/versions/</c>: creates the version from a validated upload. With a source file the request
    /// is multipart (the API takes source code only that way).
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="addon"></param>
    /// <param name="request"></param>
    /// <param name="source">Source code archive, or null.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<AmoVersionRead> CreateVersionAsync(string secretJson, string addon, AmoVersionCreate request, AmoFile source, CancellationToken cancellationToken);

    /// <summary>
    /// <c>PATCH /addons/addon/{id}/versions/{versionId}/</c> with release notes per locale and notes to the reviewer.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="addon"></param>
    /// <param name="versionId"></param>
    /// <param name="releaseNotes">Locale to text; a null text removes that locale.</param>
    /// <param name="approvalNotes">Notes to the reviewer, or null to keep them.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<AmoVersionRead> UpdateVersionAsync(string secretJson, string addon, long versionId, IReadOnlyDictionary<string, string> releaseNotes, string approvalNotes, CancellationToken cancellationToken);

    /// <summary>
    /// <c>GET /ratings/rating/?addon={id}</c>, newest first, up to <paramref name="maxPages"/> pages of 50, with the
    /// developer replies.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="addon"></param>
    /// <param name="maxPages"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IReadOnlyList<StoreReviewRecord>> ListRatingsAsync(string secretJson, string addon, int maxPages, CancellationToken cancellationToken);

    /// <summary>
    /// <c>POST /ratings/rating/{id}/reply/</c>: the developer reply (an existing reply is edited).
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="ratingId"></param>
    /// <param name="body"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<StoreReplyRecord> ReplyAsync(string secretJson, string ratingId, string body, CancellationToken cancellationToken);
}

/// <summary>
/// The account of an AMO API key.
/// </summary>
/// <param name="Id">User id.</param>
/// <param name="Username">User name.</param>
/// <param name="Name">Display name.</param>
public sealed record AmoProfileRead(long Id, string Username, string Name);

/// <summary>
/// A file sent to AMO.
/// </summary>
/// <param name="Content">The bytes (not disposed by the client).</param>
/// <param name="FileName">File name.</param>
/// <param name="ContentType">Media type.</param>
/// <param name="Length">Size in bytes.</param>
public sealed record AmoFile(Stream Content, string FileName, string ContentType, long Length);

/// <summary>
/// A preview (screenshot) of an add-on.
/// </summary>
/// <param name="Id">Preview id.</param>
/// <param name="ImageUrl">Full size image.</param>
/// <param name="ThumbnailUrl">Thumbnail.</param>
/// <param name="Position">Sort position.</param>
/// <param name="Caption">Caption in the default locale.</param>
/// <param name="Width">Image width, when reported.</param>
/// <param name="Height">Image height, when reported.</param>
public sealed record AmoPreviewRead(long Id, string ImageUrl, string ThumbnailUrl, int Position, string Caption, int? Width, int? Height);

/// <summary>
/// An add-on as AMO reports it.
/// </summary>
public sealed class AmoAddonRead
{
    /// <summary>Numeric id.</summary>
    public long Id { get; set; }

    /// <summary>Guid.</summary>
    public string Guid { get; set; }

    /// <summary>Slug.</summary>
    public string Slug { get; set; }

    /// <summary>Type (<c>extension</c>, <c>statictheme</c>, ...).</summary>
    public string Type { get; set; }

    /// <summary>Status.</summary>
    public FirefoxAddonStatus Status { get; set; }

    /// <summary>Disabled by the developer.</summary>
    public bool IsDisabled { get; set; }

    /// <summary>Default locale of the listing.</summary>
    public string DefaultLocale { get; set; }

    /// <summary>Name per locale.</summary>
    public Dictionary<string, string> Name { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Summary per locale.</summary>
    public Dictionary<string, string> Summary { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Description per locale.</summary>
    public Dictionary<string, string> Description { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Homepage per locale.</summary>
    public Dictionary<string, string> Homepage { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Support site per locale.</summary>
    public Dictionary<string, string> SupportUrl { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Support e-mail per locale.</summary>
    public Dictionary<string, string> SupportEmail { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Category slugs.</summary>
    public List<string> Categories { get; set; } = [];

    /// <summary>Previews in their order.</summary>
    public List<AmoPreviewRead> Previews { get; set; } = [];

    /// <summary>Icon address.</summary>
    public string IconUrl { get; set; }

    /// <summary>The version on the add-on page.</summary>
    public AmoVersionRead CurrentVersion { get; set; }

    /// <summary>Average rating, null without ratings.</summary>
    public double? Rating { get; set; }

    /// <summary>Number of ratings.</summary>
    public long RatingCount { get; set; }

    /// <summary>Average daily users.</summary>
    public long? AverageDailyUsers { get; set; }

    /// <summary>Weekly downloads.</summary>
    public long? WeeklyDownloads { get; set; }

    /// <summary>Public page.</summary>
    public string Url { get; set; }

    /// <summary>Developer Hub page.</summary>
    public string EditUrl { get; set; }

    /// <summary>
    /// The name in <paramref name="locale"/>, the default locale or any locale.
    /// </summary>
    /// <param name="locale"></param>
    /// <returns></returns>
    public string NameIn(string locale = null)
        => (locale != null ? Name.GetValueOrDefault(locale) : null)
           ?? (DefaultLocale != null ? Name.GetValueOrDefault(DefaultLocale) : null)
           ?? Name.Values.FirstOrDefault(v => !string.IsNullOrEmpty(v));
}

/// <summary>
/// A version as AMO reports it.
/// </summary>
public sealed class AmoVersionRead
{
    /// <summary>Version id.</summary>
    public long Id { get; set; }

    /// <summary>Version string.</summary>
    public string Version { get; set; }

    /// <summary>Channel.</summary>
    public FirefoxVersionChannel Channel { get; set; }

    /// <summary>File review status.</summary>
    public FirefoxFileStatus FileStatus { get; set; }

    /// <summary>Disabled by the developer.</summary>
    public bool IsDisabled { get; set; }

    /// <summary>Release notes per locale.</summary>
    public Dictionary<string, string> ReleaseNotes { get; set; } = new(StringComparer.Ordinal);

    /// <summary>License slug or name.</summary>
    public string License { get; set; }

    /// <summary>Whether source code was uploaded (owners only).</summary>
    public bool HasSource { get; set; }

    /// <summary>When the file was created.</summary>
    public DateTime? Created { get; set; }

    /// <summary>Download address of the signed file.</summary>
    public string FileUrl { get; set; }

    /// <summary>Size of the file.</summary>
    public long? FileSize { get; set; }

    /// <summary>Permissions of the file's manifest.</summary>
    public List<string> Permissions { get; set; } = [];

    /// <summary>Data collection permissions of the file's manifest.</summary>
    public List<string> DataCollectionPermissions { get; set; } = [];

    /// <summary>Lowest Firefox version, when known.</summary>
    public string MinFirefox { get; set; }
}

/// <summary>
/// An upload and its validation.
/// </summary>
/// <param name="Uuid">Upload id.</param>
/// <param name="Processed">Whether the validation finished.</param>
/// <param name="Valid">Whether it passed.</param>
/// <param name="Submitted">Whether a version was created from it already.</param>
/// <param name="Version">Version read from the manifest.</param>
/// <param name="Errors">Number of validation errors.</param>
/// <param name="Warnings">Number of validation warnings.</param>
/// <param name="Messages">The first validation messages, as <c>type: message</c>.</param>
public sealed record AmoUploadRead(string Uuid, bool Processed, bool Valid, bool Submitted, string Version, int Errors, int Warnings, IReadOnlyList<string> Messages);

/// <summary>
/// A version to create.
/// </summary>
/// <param name="UploadUuid">The validated upload.</param>
/// <param name="ReleaseNotes">Release notes per locale.</param>
/// <param name="ApprovalNotes">Notes to the reviewer (listed versions).</param>
/// <param name="License">License slug; null keeps the previous version's.</param>
/// <param name="Compatibility">Applications (<c>firefox</c>, <c>android</c>); empty uses the manifest.</param>
public sealed record AmoVersionCreate(string UploadUuid, IReadOnlyDictionary<string, string> ReleaseNotes, string ApprovalNotes, string License, IReadOnlyList<string> Compatibility);
