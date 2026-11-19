using Storeasy.Application.Dtos.MsStoreDtos;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Content;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Storeasy.Application.Utils.Ai;

/// <summary>
/// Turns a <see cref="PreReviewInput"/> into the <see cref="PreReviewSnapshot"/> the model reads: one
/// <c>&lt;submission&gt;</c> element of named, escaped tags (app, listings per locale, settings, pricing, build, release,
/// review information, declarations, in-app products, readiness, past rejections, screenshots), cut to sensible lengths.
/// The primary locale is complete; other locales are shortened. Review contact details and the demo account are only
/// reported as present or missing, and passwords never appear.
/// </summary>
public static class PreReviewSnapshotBuilder
{
    /// <summary>
    /// Most locales written out (the primary one first).
    /// </summary>
    public const int MaxLocales = 8;

    /// <summary>
    /// Longest value of the primary locale.
    /// </summary>
    public const int PrimaryValueMaxLength = 4000;

    /// <summary>
    /// Longest value of another locale.
    /// </summary>
    public const int OtherValueMaxLength = 600;

    /// <summary>
    /// Longest pasted rejection text.
    /// </summary>
    public const int RejectionTextMaxLength = 1500;

    /// <summary>
    /// Most past rejections written out.
    /// </summary>
    public const int MaxRejections = 5;

    /// <summary>
    /// Most in-app products written out.
    /// </summary>
    public const int MaxProducts = 30;

    /// <summary>
    /// Most readiness items written out.
    /// </summary>
    public const int MaxReadinessItems = 60;

    // Review fields that hold a person's details or the demo account: only their presence is told.
    private static readonly HashSet<string> _presenceOnlyReviewFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "contactFirstName", "contactLastName", "contactPhone", "contactEmail", "demoAccountName", "demoAccountPassword",
    };

    // The phone and tablet classes reviewers look at first, per store.
    private static readonly string[] _appleClassOrder = ["APP_IPHONE_67", "APP_IPHONE_65", "APP_IPHONE_61", "APP_IPHONE_58", "APP_IPHONE_55", "APP_IPAD_PRO_3GEN_129", "APP_IPAD_PRO_129", "APP_IPAD_PRO_3GEN_11", "APP_DESKTOP", "APP_APPLE_VISION_PRO", "APP_APPLE_TV", "APP_WATCH_ULTRA"];
    private static readonly string[] _googleClassOrder = ["phoneScreenshots", "featureGraphic", "tenInchScreenshots", "sevenInchScreenshots", "icon", "wearScreenshots", "tvScreenshots", "tvBanner"];

    /// <summary>
    /// Builds the snapshot.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static PreReviewSnapshot Build(PreReviewInput input)
    {
        var builder = new StringBuilder();
        var content = input.Content;
        var ms = input.MsContent;

        Open(builder, "submission");
        WriteApp(builder, input.App, content, ms);

        if (content != null)
        {
            WriteListings(builder, content);
            WriteSettings(builder, content);
            WritePricing(builder, content);
            WriteBuild(builder, content);
            WriteRelease(builder, content);
            WriteReviewInformation(builder, content);
            WriteMedia(builder, content);
        }

        if (ms != null)
            WriteMicrosoft(builder, ms);

        WriteAnswers(builder, input.Answers);
        WriteProducts(builder, input.Products, content?.PrimaryLocale ?? ms?.Listings.FirstOrDefault()?.Locale);
        WriteReadiness(builder, input);
        WriteRejections(builder, input.Rejections);
        Close(builder, "submission");

        var text = builder.ToString();
        var candidates = content == null ? [] : ImageCandidates(content);

        return new PreReviewSnapshot
        {
            Text = text,
            VersionString = content?.VersionString ?? ms?.Packages.Select(p => p.Version).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)),
            ImageCandidates = candidates,
            Hash = Hash(text, candidates.Select(c => c.Checksum ?? c.Url)),
        };
    }

    /// <summary>
    /// The screenshots worth sending, in order: the primary locale (or the first one) only, the phone class first (up to six
    /// of its screenshots), then the other classes (up to three each) in the order a reviewer looks at them.
    /// </summary>
    /// <param name="content"></param>
    /// <returns></returns>
    public static List<PreReviewImageCandidate> ImageCandidates(StoreContentSnapshot content)
    {
        var screenshots = content.Media.Where(m => m.Kind == MediaKind.Screenshot && m.Items.Count > 0).ToList();

        if (screenshots.Count == 0)
            return [];

        // Play graphics have no locale in some reads; they count for every locale.
        var locale = screenshots.Any(m => m.Locale == content.PrimaryLocale) ? content.PrimaryLocale : screenshots.Select(m => m.Locale).FirstOrDefault(l => l != null);
        var order = content.Store == StoreKind.Google ? _googleClassOrder : _appleClassOrder;
        var sets = screenshots.Where(m => m.Locale == locale || m.Locale == null)
                              .OrderBy(m => Array.IndexOf(order, m.ClassKey) is var index && index >= 0 ? index : order.Length)
                              .ThenBy(m => m.ClassKey, StringComparer.Ordinal)
                              .ToList();

        var candidates = new List<PreReviewImageCandidate>();

        for (var setIndex = 0; setIndex < sets.Count; setIndex++)
        {
            var set = sets[setIndex];
            var limit = setIndex == 0 ? 6 : 3;

            foreach (var (item, position) in set.Items.Select((item, position) => (item, position)).Take(limit))
            {
                var url = item.ImageUrl ?? item.Url;

                if (string.IsNullOrWhiteSpace(url))
                    continue;

                candidates.Add(new PreReviewImageCandidate($"{set.ClassKey}, {set.Locale ?? "all locales"}, position {position + 1} of {set.Items.Count}", url, item.Checksum));
            }
        }

        return candidates;
    }

    private static void WriteApp(StringBuilder builder, StoreAppContext app, StoreContentSnapshot content, MsContentDto ms)
    {
        Open(builder, "app");
        PromptTags.Tag(builder, "store", StoreName(app.Store));
        PromptTags.Tag(builder, "app_name", app.AppName);

        if (content != null)
        {
            PromptTags.Tag(builder, "platform", content.Platform);
            PromptTags.Tag(builder, "version", content.VersionString);
            PromptTags.Tag(builder, "version_state", content.VersionState);
            PromptTags.Tag(builder, "update_of_live_app", content.HasLiveVersion switch { true => "yes", false => "no, first release", _ => null });
            PromptTags.Tag(builder, "primary_locale", content.PrimaryLocale);
            PromptTags.Tag(builder, "locales", string.Join(", ", content.Locales));
        }

        if (ms != null)
        {
            PromptTags.Tag(builder, "product_kind", ms.Kind.ToString());
            PromptTags.Tag(builder, "submission_target", ms.Target.ToString());
            PromptTags.Tag(builder, "submission_status", ms.SubmissionStatus?.ToString());
            PromptTags.Tag(builder, "locales", string.Join(", ", ms.Listings.Select(l => l.Locale)));
        }

        var profile = app.Profile;

        if (profile != null)
        {
            PromptTags.Tag(builder, "device_families", Join(profile.DeviceFamilies));
            PromptTags.Tag(builder, "regions", Join(profile.Regions));
            PromptTags.Tag(builder, "app_type", profile.AppType);
            PromptTags.Tag(builder, "features", Join(profile.Features));
            PromptTags.Tag(builder, "target_sdk", profile.TargetSdk?.ToString(CultureInfo.InvariantCulture));
        }

        Close(builder, "app");
    }

    private static void WriteListings(StringBuilder builder, StoreContentSnapshot content)
    {
        var localized = content.Values.Select(v => (Key: ContentKey.Parse(v.Key), v.Value))
                                      .Where(v => v.Key.Locale != null && v.Key.Group is ContentGroups.AppInfo or ContentGroups.Version or ContentGroups.Listing && !string.IsNullOrWhiteSpace(v.Value))
                                      .ToList();

        var locales = localized.Select(v => v.Key.Locale).Distinct(StringComparer.Ordinal).ToList();
        var primary = locales.Contains(content.PrimaryLocale) ? content.PrimaryLocale : locales.FirstOrDefault();
        var ordered = locales.OrderBy(l => l == primary ? 0 : 1).ThenBy(l => l, StringComparer.Ordinal).ToList();

        foreach (var locale in ordered.Take(MaxLocales))
        {
            var isPrimary = locale == primary;

            Open(builder, "listing");
            PromptTags.Tag(builder, "locale", locale);
            PromptTags.Tag(builder, "primary", isPrimary ? "yes" : "no, shortened");

            foreach (var (key, value) in localized.Where(v => v.Key.Locale == locale).OrderBy(v => FieldOrder(content.Store, v.Key.Field)))
                PromptTags.Tag(builder, Snake(key.Field), value, isPrimary ? PrimaryValueMaxLength : OtherValueMaxLength);

            Close(builder, "listing");
        }

        if (ordered.Count > MaxLocales)
            PromptTags.Tag(builder, "other_locales_not_shown", string.Join(", ", ordered.Skip(MaxLocales)));
    }

    private static void WriteSettings(StringBuilder builder, StoreContentSnapshot content)
    {
        var values = content.Values.Select(v => (Key: ContentKey.Parse(v.Key), v.Value))
                                   .Where(v => v.Key.Locale == null && !string.IsNullOrWhiteSpace(v.Value))
                                   .ToList();

        var settings = values.Where(v => v.Key.Group is ContentGroups.App or ContentGroups.Details or ContentGroups.AppInfo or ContentGroups.Version).OrderBy(v => v.Key.Field, StringComparer.Ordinal).ToList();
        var ageRating = values.Where(v => v.Key.Group == ContentGroups.AgeRating).OrderBy(v => v.Key.Field, StringComparer.Ordinal).ToList();

        if (settings.Count > 0)
        {
            Open(builder, "app_settings");

            foreach (var (key, value) in settings)
                PromptTags.Tag(builder, Snake(key.Field), value, PrimaryValueMaxLength);

            Close(builder, "app_settings");
        }

        if (ageRating.Count > 0)
            PromptTags.Tag(builder, "age_rating_answers", string.Join("; ", ageRating.Select(v => $"{v.Key.Field}={v.Value}")), PrimaryValueMaxLength);
    }

    private static void WritePricing(StringBuilder builder, StoreContentSnapshot content)
    {
        var territories = content.Values.Select(v => (Key: ContentKey.Parse(v.Key), v.Value)).Where(v => v.Key.Group == ContentGroups.Availability).ToList();
        var available = territories.Where(t => t.Value == "true").Select(t => t.Key.Field).Order(StringComparer.Ordinal).ToList();
        var baseTerritory = content.Get(ContentKey.Of(ContentGroups.Pricing, null, "baseTerritory"));

        if (content.Prices.Count == 0 && territories.Count == 0 && baseTerritory == null)
            return;

        Open(builder, "pricing");
        PromptTags.Tag(builder, "base_territory", baseTerritory);
        PromptTags.Tag(builder, "prices", string.Join("; ", content.Prices.Take(10).Select(p => $"{p.Territory ?? "base"}: {p.CustomerPrice ?? "?"}{(p.StartDate == null ? string.Empty : $" from {p.StartDate:yyyy-MM-dd}")}")));

        if (territories.Count > 0)
            PromptTags.Tag(builder, "available_territories", $"{available.Count} of {territories.Count}: {string.Join(", ", available.Take(40))}{(available.Count > 40 ? ", ..." : string.Empty)}");

        PromptTags.Tag(builder, "available_in_new_territories", YesNo(content.AvailableInNewTerritories));
        Close(builder, "pricing");
    }

    private static void WriteBuild(StringBuilder builder, StoreContentSnapshot content)
    {
        if (content.Build == null)
        {
            if (content.Store == StoreKind.Apple)
                PromptTags.Tag(builder, "build", "no build attached to the version");

            return;
        }

        Open(builder, "build");
        PromptTags.Tag(builder, "build_number", content.Build.BuildNumber);
        PromptTags.Tag(builder, "processing_state", content.Build.ProcessingState.ToString());
        PromptTags.Tag(builder, "uses_non_exempt_encryption", YesNo(content.Build.UsesNonExemptEncryption) ?? "not answered");
        Close(builder, "build");
    }

    private static void WriteRelease(StringBuilder builder, StoreContentSnapshot content)
    {
        var version = content.Versions.FirstOrDefault(v => v.Id == content.VersionId);

        if (version != null)
        {
            Open(builder, "release");
            PromptTags.Tag(builder, "release_type", version.ReleaseType);
            PromptTags.Tag(builder, "earliest_release_date", version.EarliestReleaseDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            PromptTags.Tag(builder, "phased_release", version.PhasedReleaseState);
            Close(builder, "release");
        }

        foreach (var track in content.Tracks.Where(t => t.Releases.Count > 0))
        {
            Open(builder, "play_track");
            PromptTags.Tag(builder, "track", track.Track);

            foreach (var release in track.Releases.Take(3))
            {
                var notes = release.ReleaseNotes.FirstOrDefault(n => n.Language == content.PrimaryLocale) ?? release.ReleaseNotes.FirstOrDefault();

                PromptTags.Tag(builder,
                               "release",
                               $"{release.Name ?? "unnamed"}: {release.Status}, version codes {Join(release.VersionCodes) ?? "none"}{(release.UserFraction == null ? string.Empty : $", rollout {release.UserFraction.Value.ToString("P0", CultureInfo.InvariantCulture)}")}");

                if (notes != null)
                    PromptTags.Tag(builder, "release_notes", $"{notes.Language}: {notes.Text}", OtherValueMaxLength);
            }

            Close(builder, "play_track");
        }
    }

    private static void WriteReviewInformation(StringBuilder builder, StoreContentSnapshot content)
    {
        var review = content.Values.Select(v => (Key: ContentKey.Parse(v.Key), v.Value)).Where(v => v.Key.Group == ContentGroups.Review).ToList();

        if (review.Count == 0)
            return;

        Open(builder, "review_information");

        string Value(string field) => review.FirstOrDefault(v => string.Equals(v.Key.Field, field, StringComparison.OrdinalIgnoreCase)).Value;

        var contactParts = new[] { "contactFirstName", "contactLastName", "contactPhone", "contactEmail" }.Count(f => !string.IsNullOrWhiteSpace(Value(f)));

        PromptTags.Tag(builder, "contact_details", contactParts == 4 ? "provided" : contactParts == 0 ? "missing" : "partly provided");
        PromptTags.Tag(builder, "demo_account_required", Value("demoAccountRequired"));
        PromptTags.Tag(builder, "demo_account_name", string.IsNullOrWhiteSpace(Value("demoAccountName")) ? "missing" : "provided");
        PromptTags.Tag(builder, "demo_account_password", content.StoreDemoPasswordSet ? "set" : "not set");

        foreach (var (key, value) in review.Where(v => !_presenceOnlyReviewFields.Contains(v.Key.Field) && !string.Equals(v.Key.Field, "demoAccountRequired", StringComparison.OrdinalIgnoreCase)))
            PromptTags.Tag(builder, Snake(key.Field), value, PrimaryValueMaxLength);

        Close(builder, "review_information");
    }

    private static void WriteMedia(StringBuilder builder, StoreContentSnapshot content)
    {
        if (content.Media.Count == 0)
            return;

        Open(builder, "media_sets");

        foreach (var set in content.Media.OrderBy(m => m.Kind).ThenBy(m => m.Locale, StringComparer.Ordinal).ThenBy(m => m.ClassKey, StringComparer.Ordinal))
        {
            var sizes = set.Items.Where(i => i.Width != null && i.Height != null).Select(i => $"{i.Width}x{i.Height}").Distinct().Take(3);

            PromptTags.Tag(builder, set.Kind == MediaKind.Preview ? "preview_set" : "screenshot_set", $"{set.ClassKey}, {set.Locale ?? "all locales"}: {set.Items.Count} items{(sizes.Any() ? $" ({string.Join(", ", sizes)})" : string.Empty)}");
        }

        Close(builder, "media_sets");
    }

    private static void WriteMicrosoft(StringBuilder builder, MsContentDto ms)
    {
        var primary = ms.Listings.FirstOrDefault()?.Locale;

        foreach (var listing in ms.Listings.Take(MaxLocales))
        {
            var max = listing.Locale == primary ? PrimaryValueMaxLength : OtherValueMaxLength;

            Open(builder, "listing");
            PromptTags.Tag(builder, "locale", listing.Locale);
            PromptTags.Tag(builder, "primary", listing.Locale == primary ? "yes" : "no, shortened");
            PromptTags.Tag(builder, "title", listing.Title, max);
            PromptTags.Tag(builder, "short_description", listing.ShortDescription, max);
            PromptTags.Tag(builder, "description", listing.Description, max);
            PromptTags.Tag(builder, "whats_new", listing.WhatsNew, max);
            PromptTags.Tag(builder, "features", Join(listing.Features), max);
            PromptTags.Tag(builder, "search_terms", Join(listing.SearchTerms), max);
            Close(builder, "listing");
        }

        if (ms.Images.Count > 0)
        {
            Open(builder, "images");

            foreach (var group in ms.Images.GroupBy(i => (i.Locale, i.ImageType)).Take(40))
                PromptTags.Tag(builder, "image_set", $"{group.Key.ImageType}, {group.Key.Locale}: {group.Count()} images{(group.Any(i => !string.IsNullOrWhiteSpace(i.Description)) ? $", captions: {string.Join(" | ", group.Select(i => i.Description).Where(d => !string.IsNullOrWhiteSpace(d)).Take(5))}" : string.Empty)}", OtherValueMaxLength);

            Close(builder, "images");
        }

        if (ms.Pricing != null)
        {
            Open(builder, "pricing");
            PromptTags.Tag(builder, "pricing_model", ms.Pricing.PricingModel);
            PromptTags.Tag(builder, "price_id", ms.Pricing.PriceId);
            PromptTags.Tag(builder, "free_trial", ms.Pricing.FreeTrial ?? ms.Pricing.TrialPeriod);
            PromptTags.Tag(builder, "visibility", ms.Pricing.Visibility ?? ms.Pricing.Discoverability);
            PromptTags.Tag(builder, "markets", ms.Pricing.Markets == null ? null : $"{ms.Pricing.Markets.Count}: {string.Join(", ", ms.Pricing.Markets.Take(40))}");
            Close(builder, "pricing");
        }

        if (ms.Packages.Count > 0)
            PromptTags.Tag(builder, "packages", string.Join("; ", ms.Packages.Take(10).Select(p => $"{p.FileName} {p.Version} {p.Architecture}")));
    }

    private static void WriteAnswers(StringBuilder builder, Dictionary<SavedAnswerKind, string> answers)
    {
        if (answers == null || answers.Count == 0)
            return;

        Open(builder, "declarations");

        if (SavedAnswerDocuments.Read<PrivacyLabelsAnswers>(answers.GetValueOrDefault(SavedAnswerKind.PrivacyLabels)) is { } privacy)
        {
            Open(builder, "app_privacy_labels");
            PromptTags.Tag(builder, "collects_data", privacy.CollectsData ? "yes" : "no");
            PromptTags.Tag(builder, "privacy_policy_url", privacy.PrivacyPolicyUrl);

            foreach (var type in privacy.DataTypes.Take(40))
                PromptTags.Tag(builder, "data_type", $"{type.Category}/{type.Type}: purposes {Join(type.Purposes) ?? "none"}, linked to user {(type.LinkedToUser ? "yes" : "no")}, tracking {(type.UsedForTracking ? "yes" : "no")}");

            Close(builder, "app_privacy_labels");
        }

        if (SavedAnswerDocuments.Read<DataSafetyAnswers>(answers.GetValueOrDefault(SavedAnswerKind.DataSafety)) is { } dataSafety)
        {
            Open(builder, "data_safety");

            foreach (var row in dataSafety.Rows.Take(80))
                PromptTags.Tag(builder, "answer", $"{row.Label ?? row.QuestionId}: {row.ResponseValue ?? row.ResponseId}", OtherValueMaxLength);

            Close(builder, "data_safety");
        }

        if (SavedAnswerDocuments.Read<ContentRatingAnswers>(answers.GetValueOrDefault(SavedAnswerKind.ContentRating)) is { } rating)
            PromptTags.Tag(builder, "content_rating_category", rating.Category);

        if (SavedAnswerDocuments.Read<TargetAudienceAnswers>(answers.GetValueOrDefault(SavedAnswerKind.TargetAudience)) is { } audience)
            PromptTags.Tag(builder, "target_audience", $"age groups {Join(audience.AgeGroups) ?? "not set"}, appeals to children {(audience.AppealsToChildren ? "yes" : "no")}, contains ads {(audience.ContainsAds ? "yes" : "no")}");

        if (SavedAnswerDocuments.Read<ReviewLoginAnswers>(answers.GetValueOrDefault(SavedAnswerKind.ReviewLogin)) is { } login)
            PromptTags.Tag(builder, "saved_review_login", $"demo account required {(login.DemoAccountRequired ? "yes" : "no")}, demo account name {(string.IsNullOrWhiteSpace(login.DemoAccountName) ? "missing" : "provided")}, contact {(string.IsNullOrWhiteSpace(login.ContactEmail) && string.IsNullOrWhiteSpace(login.ContactPhone) ? "missing" : "provided")}");

        if (SavedAnswerDocuments.Read<ReviewNotesAnswers>(answers.GetValueOrDefault(SavedAnswerKind.ReviewNotes)) is { } notes)
            PromptTags.Tag(builder, "saved_review_notes", notes.Notes, PrimaryValueMaxLength);

        Close(builder, "declarations");
    }

    private static void WriteProducts(StringBuilder builder, List<PreReviewProduct> products, string primaryLocale)
    {
        if (products == null || products.Count == 0)
            return;

        Open(builder, "in_app_products");

        foreach (var product in products.Take(MaxProducts))
        {
            var localization = product.Localizations?.FirstOrDefault(l => l.Locale == primaryLocale) ?? product.Localizations?.FirstOrDefault();

            Open(builder, "product");
            PromptTags.Tag(builder, "kind", product.Kind.ToString());
            PromptTags.Tag(builder, "product_id", product.ProductId);
            PromptTags.Tag(builder, "reference_name", product.ReferenceName);
            PromptTags.Tag(builder, "state", product.State.ToString());
            PromptTags.Tag(builder, "period", product.Duration);
            PromptTags.Tag(builder, "display_name", localization?.Name, OtherValueMaxLength);
            PromptTags.Tag(builder, "description", localization?.Description, OtherValueMaxLength);
            PromptTags.Tag(builder, "prices_set", product.PriceCount.ToString(CultureInfo.InvariantCulture));
            PromptTags.Tag(builder, "review_note", product.HasReviewNote ? "written" : "missing");
            PromptTags.Tag(builder, "review_screenshot", product.HasReviewScreenshot ? "uploaded" : "missing");
            Close(builder, "product");
        }

        if (products.Count > MaxProducts)
            PromptTags.Tag(builder, "more_products", (products.Count - MaxProducts).ToString(CultureInfo.InvariantCulture));

        Close(builder, "in_app_products");
    }

    private static void WriteReadiness(StringBuilder builder, PreReviewInput input)
    {
        var report = input.Readiness;

        if (report == null)
            return;

        var items = report.Items.Where(i => i.Status is ReadinessStatus.Missing or ReadinessStatus.Warning or ReadinessStatus.Assisted or ReadinessStatus.Unknown)
                                .OrderBy(i => i.Status)
                                .ToList();

        Open(builder, "readiness_check");
        PromptTags.Tag(builder, "summary", $"{report.Ok} ok, {report.Missing} missing, {report.Warning} warnings, {report.Assisted} to do by hand, {report.Unknown} unknown; catalog {report.CatalogVersion}");

        foreach (var item in items.Take(MaxReadinessItems))
            PromptTags.Tag(builder, "item", $"{item.Status}: {item.Title ?? item.Key}{(item.Locale == null ? string.Empty : $" ({item.Locale})")}{(string.IsNullOrWhiteSpace(item.Message) ? string.Empty : $". {item.Message}")}{(item.RuleId == null ? string.Empty : $" [rule {item.RuleId}]")}", OtherValueMaxLength);

        Close(builder, "readiness_check");
    }

    private static void WriteRejections(StringBuilder builder, List<PreReviewRejection> rejections)
    {
        if (rejections == null || rejections.Count == 0)
            return;

        Open(builder, "past_rejections");

        foreach (var rejection in rejections.Take(MaxRejections))
        {
            Open(builder, "rejection");
            PromptTags.Tag(builder, "date", rejection.OpenedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            PromptTags.Tag(builder, "version", rejection.VersionString);
            PromptTags.Tag(builder, "kind", rejection.Kind.ToString());
            PromptTags.Tag(builder, "state", rejection.State.ToString());
            PromptTags.Tag(builder, "guidelines", string.Join("; ", (rejection.Guidelines ?? []).Select(g => string.IsNullOrWhiteSpace(g.Title) ? g.Code : $"{g.Code} {g.Title}")));
            PromptTags.Tag(builder, "reviewer_message", rejection.RawText, RejectionTextMaxLength);
            Close(builder, "rejection");
        }

        Close(builder, "past_rejections");
    }

    private static int FieldOrder(StoreKind store, string field)
    {
        var index = ListingFields.Of(store).Select(f => f.Field).ToList().IndexOf(field);

        return index < 0 ? int.MaxValue : index;
    }

    private static void Open(StringBuilder builder, string name) => builder.Append('<').Append(name).Append('>').Append(PromptTags.LineFeed);

    private static void Close(StringBuilder builder, string name) => builder.Append("</").Append(name).Append('>').Append(PromptTags.LineFeed);

    private static string Join(IEnumerable<string> values)
    {
        var list = values?.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();

        return list == null || list.Count == 0 ? null : string.Join(", ", list);
    }

    private static string YesNo(bool? value) => value switch { true => "yes", false => "no", _ => null };

    // camelCase field names of the stores as snake_case tag names; anything but letters and digits becomes an underscore.
    private static string Snake(string field)
    {
        var builder = new StringBuilder(field.Length + 4);

        foreach (var character in field)
        {
            if (char.IsAsciiLetterUpper(character))
            {
                if (builder.Length > 0)
                    builder.Append('_');

                builder.Append(char.ToLowerInvariant(character));
            }
            else
            {
                builder.Append(char.IsAsciiLetterOrDigit(character) ? character : '_');
            }
        }

        return builder.Length == 0 ? "field" : builder.ToString();
    }

    /// <summary>
    /// Display name of a store.
    /// </summary>
    /// <param name="store"></param>
    /// <returns></returns>
    public static string StoreName(StoreKind store) => store switch
    {
        StoreKind.Apple => "App Store",
        StoreKind.Google => "Google Play",
        StoreKind.Microsoft => "Microsoft Store",
        _ => store.ToString(),
    };

    private static string Hash(string text, IEnumerable<string> checksums)
    {
        var payload = new StringBuilder(text);

        foreach (var checksum in checksums)
            payload.Append(PromptTags.LineFeed).Append(checksum);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload.ToString())));
    }
}
