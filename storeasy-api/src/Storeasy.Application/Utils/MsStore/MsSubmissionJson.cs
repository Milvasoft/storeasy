using Storeasy.Application.Dtos.MsStoreDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.JsonModels;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Storeasy.Application.Utils.MsStore;

/// <summary>
/// Reads and edits the JSON resources of the Microsoft Store APIs: MSIX app, flight and add-on submissions
/// (<c>listings.{locale}.baseListing</c>, <c>applicationPackages</c> / <c>flightPackages</c>, <c>pricing</c>,
/// <c>packageDeliveryOptions</c>, <c>status</c>, <c>statusDetails</c>) and the MSI/EXE metadata modules
/// (<c>listings</c>, <c>availability</c>). Edits keep every field the API returned, since a submission PUT replaces the
/// whole resource.
/// </summary>
public static class MsSubmissionJson
{
    /// <summary>
    /// Fields that change without anyone editing the content (server side processing, ids, upload address); left out of
    /// <see cref="ContentHash"/>.
    /// </summary>
    private static readonly HashSet<string> _volatileFields = ["status", "statusDetails", "fileUploadUrl", "fileStatus", "id", "friendlyName"];

    /// <summary>
    /// Image types that are screenshots of the desktop device family (at most 10 per language).
    /// </summary>
    public const string DesktopScreenshotType = "Screenshot";

    /// <summary>
    /// Image types of the other device families' screenshots (at most 8 per language each).
    /// </summary>
    public static readonly IReadOnlySet<string> OtherScreenshotTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "MobileScreenshot", "XboxScreenshot", "SurfaceHubScreenshot", "HoloLensScreenshot" };

    /// <summary>
    /// Maps an API status string (MSIX status or MSI/EXE publishing status) to <see cref="MsSubmissionStatus"/>.
    /// </summary>
    /// <param name="status"></param>
    /// <returns></returns>
    public static MsSubmissionStatus ParseStatus(string status) => status?.ToUpperInvariant() switch
    {
        "PENDINGCOMMIT" => MsSubmissionStatus.PendingCommit,
        "COMMITSTARTED" => MsSubmissionStatus.CommitStarted,
        "COMMITFAILED" => MsSubmissionStatus.CommitFailed,
        "PREPROCESSING" => MsSubmissionStatus.PreProcessing,
        "PREPROCESSINGFAILED" => MsSubmissionStatus.PreProcessingFailed,
        "CERTIFICATION" => MsSubmissionStatus.Certification,
        "CERTIFICATIONFAILED" => MsSubmissionStatus.CertificationFailed,
        "RELEASE" => MsSubmissionStatus.Release,
        "RELEASEFAILED" => MsSubmissionStatus.ReleaseFailed,
        "PENDINGPUBLICATION" => MsSubmissionStatus.PendingPublication,
        "PUBLISHING" => MsSubmissionStatus.Publishing,
        "PUBLISHED" => MsSubmissionStatus.Published,
        "PUBLISHFAILED" => MsSubmissionStatus.PublishFailed,
        "CANCELED" or "CANCELLED" => MsSubmissionStatus.Canceled,
        "INPROGRESS" => MsSubmissionStatus.InProgress,
        "FAILED" => MsSubmissionStatus.Failed,
        _ => MsSubmissionStatus.Unknown,
    };

    /// <summary>
    /// Whether a submission in <paramref name="status"/> still occupies its target's single active submission slot.
    /// </summary>
    /// <param name="status"></param>
    /// <returns></returns>
    public static bool IsActive(MsSubmissionStatus status) => status is MsSubmissionStatus.CommitStarted
                                                                      or MsSubmissionStatus.PreProcessing
                                                                      or MsSubmissionStatus.Certification
                                                                      or MsSubmissionStatus.Release
                                                                      or MsSubmissionStatus.PendingPublication
                                                                      or MsSubmissionStatus.Publishing
                                                                      or MsSubmissionStatus.InProgress;

    /// <summary>
    /// Maps a rollout status string.
    /// </summary>
    /// <param name="status"></param>
    /// <returns></returns>
    public static MsRolloutStatus? ParseRolloutStatus(string status) => status switch
    {
        "PackageRolloutNotStarted" => MsRolloutStatus.NotStarted,
        "PackageRolloutInProgress" => MsRolloutStatus.InProgress,
        "PackageRolloutComplete" => MsRolloutStatus.Complete,
        "PackageRolloutStopped" => MsRolloutStatus.Stopped,
        _ => null,
    };

    /// <summary>
    /// Reads <c>statusDetails</c> (errors, warnings, certification reports); details are redacted.
    /// </summary>
    /// <param name="details"></param>
    /// <returns></returns>
    public static MsStatusDetails ReadStatusDetails(JsonNode details)
    {
        var result = new MsStatusDetails();

        if (details is not JsonObject obj)
            return result;

        result.Errors = ReadItems(obj["errors"]);
        result.Warnings = ReadItems(obj["warnings"]);
        result.CertificationReports = [.. Array(obj["certificationReports"]).Select(r => new MsCertificationReport
        {
            Date = ReadDate(r, "date"),
            ReportUrl = String(r, "reportUrl"),
        })];

        return result;
    }

    /// <summary>
    /// Reads a list of <c>{ code, details }</c> (or MSI/EXE <c>{ code, message }</c>) items.
    /// </summary>
    /// <param name="items"></param>
    /// <returns></returns>
    public static List<MsStatusItem> ReadItems(JsonNode items) => [.. Array(items).Select(i => new MsStatusItem
    {
        Code = String(i, "code"),
        Details = SecretRedactor.RedactAndTrim(String(i, "details") ?? String(i, "message"), 1000),
    })];

    /// <summary>
    /// Fingerprint of a submission's content: SHA-256 (hex) of its canonical JSON without the volatile fields. Equal
    /// fingerprints mean nobody edited the content between two reads.
    /// </summary>
    /// <param name="document"></param>
    /// <returns></returns>
    public static string ContentHash(JsonNode document)
    {
        if (document == null)
            return null;

        var builder = new StringBuilder();

        WriteCanonical(document, builder);

        return Hash(builder.ToString());
    }

    /// <summary>
    /// Package file extensions the MSIX submission API accepts.
    /// </summary>
    public static readonly IReadOnlySet<string> PackageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".msix", ".msixbundle", ".msixupload", ".appx", ".appxbundle", ".appxupload" };

    /// <summary>
    /// Folder of the listing images in a submission's ZIP archive.
    /// </summary>
    public const string ImagesFolder = "images";

    /// <summary>
    /// Folder of the packages in a submission's ZIP archive.
    /// </summary>
    public const string PackagesFolder = "packages";

    /// <summary>
    /// Path of an uploaded asset in a submission's ZIP archive (and so its <c>fileName</c> in the submission). The asset id
    /// keeps two uploads with the same name apart.
    /// </summary>
    /// <param name="folder"></param>
    /// <param name="assetId"></param>
    /// <param name="fileName"></param>
    /// <returns></returns>
    public static string ZipEntryName(string folder, long assetId, string fileName)
    {
        var name = Path.GetFileName(fileName ?? string.Empty);

        name = new string([.. name.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_')]);

        return $"{folder}/{assetId}_{name}";
    }

    /// <summary>
    /// SHA-256 (lower case hex) of a text.
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    public static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text ?? string.Empty)));

    /// <summary>
    /// The sanitized original file name of a <see cref="ZipEntryName"/> in <paramref name="folder"/> (the part after the
    /// asset id), or null when <paramref name="entryName"/> is not such a name.
    /// </summary>
    /// <param name="folder"></param>
    /// <param name="entryName"></param>
    /// <returns></returns>
    public static string OriginalName(string folder, string entryName)
    {
        var prefix = $"{folder}/";

        if (entryName?.StartsWith(prefix, StringComparison.Ordinal) != true)
            return null;

        var rest = entryName[prefix.Length..];
        var separator = rest.IndexOf('_');

        return separator > 0 && rest[..separator].All(char.IsAsciiDigit) && separator < rest.Length - 1 ? rest[(separator + 1)..] : null;
    }

    #region MSIX submissions

    /// <summary>
    /// Reads the base listings of an app submission, ordered by language.
    /// </summary>
    /// <param name="submission"></param>
    /// <returns></returns>
    public static List<MsListingDto> ReadListings(JsonObject submission)
    {
        if (submission?["listings"] is not JsonObject listings)
            return [];

        return [.. listings.Where(l => l.Value is JsonObject)
                           .Select(l => (Locale: l.Key, Base: l.Value["baseListing"] as JsonObject))
                           .Where(l => l.Base != null)
                           .Select(l => new MsListingDto
                           {
                               Locale = l.Locale.ToLowerInvariant(),
                               Title = String(l.Base, "title"),
                               Description = String(l.Base, "description"),
                               ShortDescription = String(l.Base, "shortDescription"),
                               WhatsNew = String(l.Base, "releaseNotes"),
                               Features = Strings(l.Base["features"]),
                               SearchTerms = Strings(l.Base["keywords"]),
                           })
                           .OrderBy(l => l.Locale, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Writes the non null fields of <paramref name="listing"/> into the submission's base listing of its language,
    /// creating the listing when the language is new.
    /// </summary>
    /// <param name="submission"></param>
    /// <param name="listing"></param>
    public static void WriteListing(JsonObject submission, MsListingDto listing)
    {
        var baseListing = BaseListing(submission, listing.Locale, create: true);

        SetIfNotNull(baseListing, "title", listing.Title);
        SetIfNotNull(baseListing, "description", listing.Description);
        SetIfNotNull(baseListing, "shortDescription", listing.ShortDescription);
        SetIfNotNull(baseListing, "releaseNotes", listing.WhatsNew);

        if (listing.Features != null)
            baseListing["features"] = new JsonArray([.. listing.Features.Select(f => (JsonNode)JsonValue.Create(f))]);

        if (listing.SearchTerms != null)
            baseListing["keywords"] = new JsonArray([.. listing.SearchTerms.Select(f => (JsonNode)JsonValue.Create(f))]);
    }

    /// <summary>
    /// Writes <paramref name="releaseNotes"/> into <c>releaseNotes</c> of every base listing the submission has (the
    /// "What's new" text of each language). No language is added and nothing else of a listing changes.
    /// </summary>
    /// <param name="submission"></param>
    /// <param name="releaseNotes"></param>
    /// <returns>The languages written.</returns>
    public static List<string> WriteReleaseNotes(JsonObject submission, string releaseNotes)
    {
        if (submission?["listings"] is not JsonObject listings || releaseNotes == null)
            return [];

        var written = new List<string>();

        foreach (var (locale, listing) in listings)
        {
            if (listing?["baseListing"] is not JsonObject baseListing)
                continue;

            baseListing["releaseNotes"] = releaseNotes;
            written.Add(locale);
        }

        return written;
    }

    /// <summary>
    /// Reads the listing images of every language (the ones marked for deletion are left out).
    /// </summary>
    /// <param name="submission"></param>
    /// <returns></returns>
    public static List<MsImageDto> ReadImages(JsonObject submission)
    {
        if (submission?["listings"] is not JsonObject listings)
            return [];

        return [.. listings.Where(l => l.Value?["baseListing"] is JsonObject)
                           .SelectMany(l => Array(l.Value["baseListing"]["images"]).Where(i => String(i, "fileStatus") != "PendingDelete").Select(i => new MsImageDto
                           {
                               Locale = l.Key.ToLowerInvariant(),
                               Id = String(i, "id"),
                               FileName = String(i, "fileName"),
                               ImageType = String(i, "imageType"),
                               Description = String(i, "description"),
                           }))
                           .OrderBy(i => i.Locale, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Marks images of a language for deletion (by file name) and adds new ones (uploaded with the submission's ZIP).
    /// </summary>
    /// <param name="submission"></param>
    /// <param name="locale"></param>
    /// <param name="removeFileNames"></param>
    /// <param name="add">New images: file name in the ZIP, image type, caption.</param>
    public static void WriteImages(JsonObject submission, string locale, IReadOnlyCollection<string> removeFileNames, IEnumerable<(string FileName, string ImageType, string Description)> add)
    {
        var baseListing = BaseListing(submission, locale, create: true);

        if (baseListing["images"] is not JsonArray images)
        {
            images = [];
            baseListing["images"] = images;
        }

        foreach (var image in images.OfType<JsonObject>().Where(i => removeFileNames.Contains(String(i, "fileName"))))
            image["fileStatus"] = "PendingDelete";

        foreach (var (fileName, imageType, description) in add)
        {
            images.Add(new JsonObject
            {
                ["fileName"] = fileName,
                ["fileStatus"] = "PendingUpload",
                ["imageType"] = imageType,
                ["description"] = description ?? string.Empty,
            });
        }
    }

    /// <summary>
    /// Name of the package array of a submission target.
    /// </summary>
    /// <param name="target"></param>
    /// <returns></returns>
    public static string PackagesField(MsSubmissionTarget target) => target == MsSubmissionTarget.Flight ? "flightPackages" : "applicationPackages";

    /// <summary>
    /// Reads the packages of an app or flight submission (the ones marked for deletion are left out).
    /// </summary>
    /// <param name="submission"></param>
    /// <param name="target"></param>
    /// <returns></returns>
    public static List<MsPackageDto> ReadPackages(JsonObject submission, MsSubmissionTarget target)
        => [.. Array(submission?[PackagesField(target)]).Where(p => String(p, "fileStatus") != "PendingDelete").Select(p => new MsPackageDto
        {
            Id = String(p, "id"),
            FileName = String(p, "fileName"),
            Version = String(p, "version"),
            Architecture = String(p, "architecture"),
        })];

    /// <summary>
    /// Marks packages for deletion (by file name) and adds new ones (uploaded with the submission's ZIP). The existing
    /// packages stay as they are. Adding is idempotent for a retry on the same draft: a file name already in the list is
    /// not added again, and a <c>PendingUpload</c> entry storeasy staged earlier for the same original file (another
    /// asset id, see <see cref="ZipEntryName"/>) is replaced, since its file was never sent.
    /// </summary>
    /// <param name="submission"></param>
    /// <param name="target"></param>
    /// <param name="removeFileNames"></param>
    /// <param name="addFileNames"></param>
    public static void WritePackages(JsonObject submission, MsSubmissionTarget target, IReadOnlyCollection<string> removeFileNames, IEnumerable<string> addFileNames)
    {
        var field = PackagesField(target);

        if (submission[field] is not JsonArray packages)
        {
            packages = [];
            submission[field] = packages;
        }

        foreach (var package in packages.OfType<JsonObject>().Where(p => removeFileNames.Contains(String(p, "fileName"))))
            package["fileStatus"] = "PendingDelete";

        foreach (var fileName in addFileNames)
        {
            var original = OriginalName(PackagesFolder, fileName);
            var stale = packages.OfType<JsonObject>()
                                .Where(p => String(p, "fileStatus") == "PendingUpload"
                                            && String(p, "fileName") != fileName
                                            && original != null
                                            && OriginalName(PackagesFolder, String(p, "fileName")) == original)
                                .ToList();

            foreach (var package in stale)
                packages.Remove(package);

            if (packages.OfType<JsonObject>().Any(p => String(p, "fileName") == fileName))
                continue;

            packages.Add(new JsonObject
            {
                ["fileName"] = fileName,
                ["fileStatus"] = "PendingUpload",
                ["minimumDirectXVersion"] = "None",
                ["minimumSystemRam"] = "None",
            });
        }
    }

    /// <summary>
    /// Reads the gradual rollout settings of a submission.
    /// </summary>
    /// <param name="submission"></param>
    /// <returns></returns>
    public static MsRolloutDto ReadRollout(JsonObject submission)
    {
        if (submission?["packageDeliveryOptions"]?["packageRollout"] is not JsonObject rollout)
            return null;

        return new MsRolloutDto
        {
            IsPackageRollout = Bool(rollout, "isPackageRollout"),
            Percentage = Double(rollout, "packageRolloutPercentage") ?? 0,
            Status = ParseRolloutStatus(String(rollout, "packageRolloutStatus")),
            FallbackSubmissionId = String(rollout, "fallbackSubmissionId"),
        };
    }

    /// <summary>
    /// Turns the gradual rollout of a draft on with <paramref name="percentage"/>, or off when null.
    /// </summary>
    /// <param name="submission"></param>
    /// <param name="percentage"></param>
    public static void WriteRollout(JsonObject submission, double? percentage)
    {
        if (submission["packageDeliveryOptions"] is not JsonObject options)
        {
            options = [];
            submission["packageDeliveryOptions"] = options;
        }

        if (options["packageRollout"] is not JsonObject rollout)
        {
            rollout = [];
            options["packageRollout"] = rollout;
        }

        rollout["isPackageRollout"] = percentage.HasValue;
        rollout["packageRolloutPercentage"] = percentage ?? 0;
    }

    /// <summary>
    /// Makes a new app, flight or add-on submission publish as soon as it passes certification: sets
    /// <c>targetPublishMode</c> to <c>Immediate</c> and removes <c>targetPublishDate</c>. Partner Center clones a new
    /// submission from the last published one, so it inherits that one's publish mode; a <c>Manual</c> mode would leave the
    /// certified submission waiting in <c>PendingPublication</c>, and the submission API has no call that publishes it
    /// (only "Publish now" in Partner Center does). Documents without a <c>targetPublishMode</c> field are left as they are.
    /// Returns whether the document changed.
    /// </summary>
    /// <param name="submission"></param>
    /// <returns></returns>
    public static bool WritePublishImmediately(JsonObject submission)
    {
        if (submission == null || !submission.ContainsKey("targetPublishMode"))
            return false;

        var changed = !string.Equals(String(submission, "targetPublishMode"), "Immediate", StringComparison.Ordinal) || submission.ContainsKey("targetPublishDate");

        submission["targetPublishMode"] = "Immediate";
        submission.Remove("targetPublishDate");

        return changed;
    }

    /// <summary>
    /// Whether the submission makes its packages a mandatory update (the API refuses such apps).
    /// </summary>
    /// <param name="submission"></param>
    /// <returns></returns>
    public static bool IsMandatoryUpdate(JsonObject submission) => submission?["packageDeliveryOptions"] is JsonObject options && Bool(options, "isMandatoryUpdate");

    /// <summary>
    /// Reads price tier, trial and visibility of an app submission, or price tier of an add-on submission.
    /// </summary>
    /// <param name="submission"></param>
    /// <returns></returns>
    public static MsPricingDto ReadPricing(JsonObject submission)
    {
        if (submission == null)
            return null;

        var pricing = submission["pricing"] as JsonObject;

        return new MsPricingDto
        {
            PriceId = String(pricing, "priceId"),
            TrialPeriod = String(pricing, "trialPeriod"),
            Visibility = String(submission, "visibility"),
        };
    }

    /// <summary>
    /// Whether the price tier the API answered is one it can write (Free, NotAvailable or TierN). Pricing Version 2
    /// products answer an unknown tier.
    /// </summary>
    /// <param name="priceId"></param>
    /// <returns></returns>
    public static bool IsKnownPriceTier(string priceId)
        => priceId != null
           && (priceId.Equals("Free", StringComparison.OrdinalIgnoreCase)
               || priceId.Equals("NotAvailable", StringComparison.OrdinalIgnoreCase)
               || (priceId.StartsWith("Tier", StringComparison.OrdinalIgnoreCase) && int.TryParse(priceId.AsSpan(4), NumberStyles.None, CultureInfo.InvariantCulture, out _)));

    /// <summary>
    /// Writes the non null price fields (MSIX app or add-on submission).
    /// </summary>
    /// <param name="submission"></param>
    /// <param name="pricing"></param>
    public static void WritePricing(JsonObject submission, MsPricingDto pricing)
    {
        if (submission["pricing"] is not JsonObject target)
        {
            target = [];
            submission["pricing"] = target;
        }

        SetIfNotNull(target, "priceId", pricing.PriceId);
        SetIfNotNull(target, "trialPeriod", pricing.TrialPeriod);
        SetIfNotNull(submission, "visibility", pricing.Visibility);
    }

    /// <summary>
    /// Reads the listings of an add-on submission.
    /// </summary>
    /// <param name="submission"></param>
    /// <returns></returns>
    public static List<MsAddonListingDto> ReadAddonListings(JsonObject submission)
    {
        if (submission?["listings"] is not JsonObject listings)
            return [];

        return [.. listings.Where(l => l.Value is JsonObject)
                           .Select(l => new MsAddonListingDto { Locale = l.Key.ToLowerInvariant(), Title = String(l.Value, "title"), Description = String(l.Value, "description") })
                           .OrderBy(l => l.Locale, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Writes listings, price tier and lifetime of an add-on submission.
    /// </summary>
    /// <param name="submission"></param>
    /// <param name="content"></param>
    public static void WriteAddonContent(JsonObject submission, MsAddonContentDto content)
    {
        if (submission["listings"] is not JsonObject listings)
        {
            listings = [];
            submission["listings"] = listings;
        }

        foreach (var listing in content.Listings ?? [])
        {
            var key = FindKey(listings, listing.Locale) ?? listing.Locale.ToLowerInvariant();

            if (listings[key] is not JsonObject target)
            {
                target = [];
                listings[key] = target;
            }

            SetIfNotNull(target, "title", listing.Title);
            SetIfNotNull(target, "description", listing.Description);
        }

        if (content.PriceId != null)
            WritePricing(submission, new MsPricingDto { PriceId = content.PriceId });

        SetIfNotNull(submission, "lifetime", content.Lifetime);
    }

    #endregion

    #region MSI/EXE metadata

    /// <summary>
    /// Reads the <c>listings</c> module of MSI/EXE metadata (an array of per language listings, or a single listing).
    /// </summary>
    /// <param name="metadata"></param>
    /// <returns></returns>
    public static List<MsListingDto> ReadProductListings(JsonObject metadata)
    {
        var node = metadata?["listings"];
        var items = node is JsonArray ? Array(node) : node is JsonObject single ? [single] : [];

        return [.. items.Where(l => String(l, "language") != null).Select(l => new MsListingDto
        {
            Locale = String(l, "language").ToLowerInvariant(),
            Description = String(l, "description"),
            ShortDescription = String(l, "shortDescription"),
            WhatsNew = String(l, "whatsNew"),
            Features = Strings(l["productFeatures"]),
            SearchTerms = Strings(l["searchTerms"]),
        }).OrderBy(l => l.Locale, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Builds the PATCH body that sets the non null fields of one MSI/EXE listing.
    /// </summary>
    /// <param name="listing"></param>
    /// <returns></returns>
    public static JsonObject ProductListingPatch(MsListingDto listing)
    {
        var body = new JsonObject { ["language"] = listing.Locale };

        SetIfNotNull(body, "description", listing.Description);
        SetIfNotNull(body, "shortDescription", listing.ShortDescription);
        SetIfNotNull(body, "whatsNew", listing.WhatsNew);

        if (listing.Features != null)
            body["productFeatures"] = new JsonArray([.. listing.Features.Select(f => (JsonNode)JsonValue.Create(f))]);

        if (listing.SearchTerms != null)
            body["searchTerms"] = new JsonArray([.. listing.SearchTerms.Select(f => (JsonNode)JsonValue.Create(f))]);

        return new JsonObject { ["listings"] = body };
    }

    /// <summary>
    /// Reads the <c>availability</c> module of MSI/EXE metadata.
    /// </summary>
    /// <param name="metadata"></param>
    /// <returns></returns>
    public static MsPricingDto ReadAvailability(JsonObject metadata)
    {
        if (metadata?["availability"] is not JsonObject availability)
            return null;

        return new MsPricingDto
        {
            Markets = Strings(availability["markets"]),
            Discoverability = String(availability, "discoverability"),
            PricingModel = String(availability, "pricing"),
            FreeTrial = String(availability, "freeTrial"),
        };
    }

    /// <summary>
    /// Builds the PATCH body that sets the non null availability fields of an MSI/EXE product.
    /// </summary>
    /// <param name="pricing"></param>
    /// <returns></returns>
    public static JsonObject AvailabilityPatch(MsPricingDto pricing)
    {
        var body = new JsonObject();

        if (pricing.Markets != null)
            body["markets"] = new JsonArray([.. pricing.Markets.Select(m => (JsonNode)JsonValue.Create(m.ToUpperInvariant()))]);

        SetIfNotNull(body, "discoverability", pricing.Discoverability);
        SetIfNotNull(body, "pricing", pricing.PricingModel);
        SetIfNotNull(body, "freeTrial", pricing.FreeTrial);

        return new JsonObject { ["availability"] = body };
    }

    #endregion

    #region Node helpers

    /// <summary>
    /// String value of a property, or null.
    /// </summary>
    /// <param name="node"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public static string String(JsonNode node, string name)
        => node is JsonObject obj && obj[name] is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;

    /// <summary>
    /// Boolean value of a property (false when missing).
    /// </summary>
    /// <param name="node"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public static bool Bool(JsonNode node, string name) => node is JsonObject obj && obj[name] is JsonValue value && value.GetValueKind() == JsonValueKind.True;

    /// <summary>
    /// Number value of a property, or null.
    /// </summary>
    /// <param name="node"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public static double? Double(JsonNode node, string name)
        => node is JsonObject obj && obj[name] is JsonValue value && value.GetValueKind() == JsonValueKind.Number ? value.GetValue<double>() : null;

    /// <summary>
    /// The items of an array node (none when the node is not an array).
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public static IEnumerable<JsonNode> Array(JsonNode node) => node is JsonArray array ? array.Where(i => i != null) : [];

    /// <summary>
    /// The string items of an array node, or null when the node is missing.
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public static List<string> Strings(JsonNode node)
        => node is JsonArray array ? [.. array.OfType<JsonValue>().Where(v => v.GetValueKind() == JsonValueKind.String).Select(v => v.GetValue<string>())] : null;

    private static DateTime? ReadDate(JsonNode node, string name)
        => DateTimeOffset.TryParse(String(node, name), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value) ? value.UtcDateTime : null;

    private static JsonObject BaseListing(JsonObject submission, string locale, bool create)
    {
        if (submission["listings"] is not JsonObject listings)
        {
            listings = [];
            submission["listings"] = listings;
        }

        var key = FindKey(listings, locale) ?? locale.ToLowerInvariant();

        if (listings[key] is not JsonObject listing)
        {
            listing = new JsonObject { ["platformOverrides"] = new JsonObject() };
            listings[key] = listing;
        }

        if (listing["baseListing"] is not JsonObject baseListing && create)
        {
            baseListing = new JsonObject { ["images"] = new JsonArray() };
            listing["baseListing"] = baseListing;
        }

        return listing["baseListing"] as JsonObject;
    }

    private static string FindKey(JsonObject obj, string locale) => obj.Select(p => p.Key).FirstOrDefault(k => string.Equals(k, locale, StringComparison.OrdinalIgnoreCase));

    private static void SetIfNotNull(JsonObject target, string name, string value)
    {
        if (value != null)
            target[name] = value;
    }

    private static void WriteCanonical(JsonNode node, StringBuilder builder)
    {
        switch (node)
        {
            case JsonObject obj:
                builder.Append('{');

                foreach (var property in obj.Where(p => !_volatileFields.Contains(p.Key)).OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    builder.Append(JsonSerializer.Serialize(property.Key)).Append(':');
                    WriteCanonical(property.Value, builder);
                    builder.Append(',');
                }

                builder.Append('}');
                break;
            case JsonArray array:
                builder.Append('[');

                foreach (var item in array)
                {
                    WriteCanonical(item, builder);
                    builder.Append(',');
                }

                builder.Append(']');
                break;
            case null:
                builder.Append("null");
                break;
            default:
                builder.Append(node.ToJsonString());
                break;
        }
    }

    #endregion
}
