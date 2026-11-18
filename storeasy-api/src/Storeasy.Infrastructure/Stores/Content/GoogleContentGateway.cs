using Storeasy.Application.Interfaces;
using Storeasy.Domain.Content;
using Storeasy.Domain.Enums;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Storeasy.Infrastructure.Stores.Content;

/// <summary>
/// Google Play content engine on androidpublisher v3 edits (store-ops <c>withEdit</c>): reads happen in an edit that is
/// deleted afterwards; writes happen in one edit that is committed at the end, or deleted when anything fails, so no edit
/// is ever left open. Data safety goes through <c>applications.dataSafety</c>, outside edits.
/// </summary>
/// <param name="http"></param>
/// <param name="fileStorage"></param>
public sealed class GoogleContentGateway(StoreHttp http, IFileStorage fileStorage) : IStoreContentGateway
{
    /// <summary>
    /// Image types storeasy reads and writes.
    /// </summary>
    public static readonly string[] ImageTypes = ["icon", "featureGraphic", "phoneScreenshots", "sevenInchScreenshots", "tenInchScreenshots"];

    private static readonly string[] _listingFields = ["title", "shortDescription", "fullDescription", "video"];
    private static readonly string[] _detailFields = ["contactEmail", "contactPhone", "contactWebsite", "defaultLanguage"];
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    private readonly StoreHttp _http = http;
    private readonly IFileStorage _fileStorage = fileStorage;

    /// <inheritdoc/>
    public StoreKind Store => StoreKind.Google;

    /// <inheritdoc/>
    public async Task<StoreContentSnapshot> ReadAsync(StoreContentTarget target, ContentParts parts, CancellationToken cancellationToken)
    {
        var api = Api(target);
        var snapshot = new StoreContentSnapshot { Store = StoreKind.Google, ExternalId = target.ExternalId, VersionEditable = true };
        var editId = await api.OpenEditAsync(cancellationToken);
        var edit = api.EditPath(editId);

        try
        {
            using (var details = await api.SendAsync(HttpMethod.Get, $"{edit}/details", null, cancellationToken))
            {
                foreach (var field in _detailFields)
                    snapshot.Values[ContentKey.Of(ContentGroups.Details, null, field)] = JsonRead.Str(details.RootElement, field);

                snapshot.PrimaryLocale = JsonRead.Str(details.RootElement, "defaultLanguage");
            }

            if ((parts & (ContentParts.Texts | ContentParts.Screenshots)) != 0)
            {
                using var listings = await api.SendAsync(HttpMethod.Get, $"{edit}/listings", null, cancellationToken);

                foreach (var listing in JsonRead.Items(listings.RootElement, "listings"))
                {
                    var language = JsonRead.Str(listing, "language");

                    snapshot.Locales.Add(language);

                    foreach (var field in _listingFields)
                        snapshot.Values[ContentKey.Of(ContentGroups.Listing, language, field)] = JsonRead.Str(listing, field);
                }
            }

            if (parts.HasFlag(ContentParts.Screenshots))
            {
                foreach (var language in snapshot.Locales)
                    snapshot.Media.AddRange(await ReadImagesAsync(api, edit, language, cancellationToken));
            }

            if ((parts & (ContentParts.Release | ContentParts.Testers)) != 0)
                await ReadTracksAsync(api, edit, parts.HasFlag(ContentParts.Testers), snapshot, cancellationToken);
        }
        finally
        {
            await api.DeleteEditAsync(editId);
        }

        return snapshot;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<StoreOption>> ListOptionsAsync(StoreContentTarget target, StoreOptionKind kind, string filter, CancellationToken cancellationToken)
        => throw new StoreCallException("Google Play offers no category, price point or territory list through the edits API.", null, false);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ApplyAsync(StoreContentTarget target, ContentChangeSet changes, IStoreJobContext context, CancellationToken cancellationToken)
    {
        var api = Api(target);
        var outcome = new List<string>();

        if (changes.DataSafetyCsv != null)
        {
            outcome.Add(await ContentSteps.RunAsync(context, "dataSafety.update", null, async () =>
            {
                using var _ = await api.SendAsync(HttpMethod.Post, $"{api.AppPath}/dataSafety", new { safetyLabels = changes.DataSafetyCsv }, cancellationToken, retry: true);
                return "Data safety form sent.";
            }, cancellationToken));
        }

        var needsEdit = changes.Fields.Count > 0 || changes.Media.Count > 0 || changes.Release != null || changes.Rollout != null || changes.Testers != null || changes.Bundle != null;

        if (!needsEdit)
            return outcome;

        var editId = await ContentSteps.RunAsync(context, "edit.insert", target.ExternalId, () => api.OpenEditAsync(cancellationToken), cancellationToken);
        var edit = api.EditPath(editId);
        var committed = false;

        try
        {
            await WriteFieldsAsync(api, edit, changes, context, outcome, cancellationToken);

            foreach (var set in changes.Media)
                outcome.Add(await ContentSteps.RunAsync(context, "images.replace", $"{set.Locale} {set.ClassKey}", () => ReplaceImagesAsync(api, edit, set, cancellationToken), cancellationToken));

            string uploadedVersionCode = null;

            if (changes.Bundle != null)
            {
                uploadedVersionCode = await UploadBundleAsync(api, edit, changes.Bundle, context, cancellationToken);
                outcome.Add($"Bundle {changes.Bundle.FileName} uploaded as version code {uploadedVersionCode}.");
            }

            if (changes.Release != null)
                outcome.Add(await ContentSteps.RunAsync(context, "track.update", changes.Release.Track, () => WriteReleaseAsync(api, edit, changes.Release, uploadedVersionCode, cancellationToken), cancellationToken));

            if (changes.Rollout != null)
                outcome.Add(await ContentSteps.RunAsync(context, "track.rollout", $"{changes.Rollout.Track} {changes.Rollout.Action}", () => WriteRolloutAsync(api, edit, changes.Rollout, cancellationToken), cancellationToken));

            if (changes.Testers != null)
            {
                outcome.Add(await ContentSteps.RunAsync(context, "testers.update", changes.Testers.Track, async () =>
                {
                    using var _ = await api.SendAsync(HttpMethod.Put, $"{edit}/testers/{Uri.EscapeDataString(changes.Testers.Track)}", new { googleGroups = changes.Testers.GoogleGroups }, cancellationToken);
                    return $"{changes.Testers.GoogleGroups.Count} tester groups.";
                }, cancellationToken));
            }

            await ContentSteps.RunAsync(context, "edit.commit", editId, async () =>
            {
                await api.CommitEditAsync(editId, cancellationToken);
                return "Edit committed.";
            }, cancellationToken);

            committed = true;
        }
        finally
        {
            if (!committed)
                await api.DeleteEditAsync(editId);
        }

        return outcome;
    }

    private GooglePlayApi Api(StoreContentTarget target)
    {
        var credentials = StoreCredentialParser.ParseGoogle(target.SecretJson, out var errorKey) ?? throw new StoreCallException(errorKey, null, false);

        return new GooglePlayApi(_http, credentials, target.ExternalId);
    }

    private static async Task<List<MediaSetState>> ReadImagesAsync(GooglePlayApi api, string edit, string language, CancellationToken cancellationToken)
    {
        var sets = new List<MediaSetState>();

        foreach (var type in ImageTypes)
        {
            using var images = await api.SendAsync(HttpMethod.Get, $"{edit}/listings/{Uri.EscapeDataString(language)}/{type}", null, cancellationToken);

            sets.Add(new MediaSetState
            {
                Kind = MediaKind.Screenshot,
                Locale = language,
                ClassKey = type,
                Items = [.. JsonRead.Items(images.RootElement, "images").Select(i => new MediaItemState
                {
                    Id = JsonRead.Str(i, "id"),
                    Checksum = JsonRead.Str(i, "sha256")?.ToLowerInvariant(),
                    Url = JsonRead.Str(i, "url"),
                    State = "COMPLETE",
                })],
            });
        }

        return sets;
    }

    private static async Task ReadTracksAsync(GooglePlayApi api, string edit, bool testers, StoreContentSnapshot snapshot, CancellationToken cancellationToken)
    {
        using (var tracks = await api.SendAsync(HttpMethod.Get, $"{edit}/tracks", null, cancellationToken))
        {
            snapshot.Tracks = [.. JsonRead.Items(tracks.RootElement, "tracks").Select(t => new TrackState
            {
                Track = JsonRead.Str(t, "track"),
                Releases = [.. JsonRead.Items(t, "releases").Select(ParseRelease)],
            })];
        }

        var production = snapshot.Tracks.FirstOrDefault(t => ChangePlanRules.IsProduction(t.Track))?.Releases.FirstOrDefault();
        var newest = snapshot.Tracks.SelectMany(t => t.Releases).SelectMany(r => r.VersionCodes).Select(c => long.TryParse(c, out var n) ? n : 0).DefaultIfEmpty(0).Max();

        snapshot.VersionString = production?.Name ?? (newest > 0 ? newest.ToString(System.Globalization.CultureInfo.InvariantCulture) : null);
        snapshot.HasLiveVersion = production?.Status is "completed" or "inProgress" or "halted";

        if (!testers)
            return;

        foreach (var track in snapshot.Tracks.Where(t => !ChangePlanRules.IsProduction(t.Track)))
        {
            try
            {
                using var document = await api.SendAsync(HttpMethod.Get, $"{edit}/testers/{Uri.EscapeDataString(track.Track)}", null, cancellationToken);

                track.GoogleGroups = [.. JsonRead.Items(document.RootElement, "googleGroups").Select(g => g.GetString())];
            }
            catch (StoreCallException ex) when (!ex.Transient)
            {
                // Testers exist only on testing tracks Play manages with groups.
            }
        }
    }

    private static TrackReleaseState ParseRelease(JsonElement release)
    {
        var targeting = JsonRead.Get(release, "countryTargeting");

        return new TrackReleaseState
        {
            Name = JsonRead.Str(release, "name"),
            VersionCodes = [.. JsonRead.Items(release, "versionCodes").Select(c => c.ValueKind == JsonValueKind.String ? c.GetString() : c.GetRawText())],
            Status = JsonRead.Str(release, "status"),
            UserFraction = JsonRead.Double(release, "userFraction"),
            ReleaseNotes = [.. JsonRead.Items(release, "releaseNotes").Select(n => new ReleaseNote(JsonRead.Str(n, "language"), JsonRead.Str(n, "text")))],
            Countries = targeting.ValueKind == JsonValueKind.Object ? [.. JsonRead.Items(targeting, "countries").Select(c => c.GetString())] : null,
            IncludeRestOfWorld = targeting.ValueKind == JsonValueKind.Object ? JsonRead.Str(targeting, "includeRestOfWorld") == "true" : null,
        };
    }

    private static async Task WriteFieldsAsync(GooglePlayApi api, string edit, ContentChangeSet changes, IStoreJobContext context, List<string> outcome, CancellationToken cancellationToken)
    {
        var details = changes.Fields.Where(f => f.Group == ContentGroups.Details).ToList();

        if (details.Count > 0)
        {
            outcome.Add(await ContentSteps.RunAsync(context, "details.patch", string.Join(", ", details.Select(d => d.Field)), async () =>
            {
                var body = new JsonObject();

                foreach (var field in details)
                    body[field.Field] = field.Value;

                using var _ = await api.SendAsync(HttpMethod.Patch, $"{edit}/details", body, cancellationToken);
                return "App details updated.";
            }, cancellationToken));
        }

        var listings = changes.Fields.Where(f => f.Group == ContentGroups.Listing).GroupBy(f => f.Locale).ToList();

        if (listings.Count == 0)
            return;

        using var current = await api.SendAsync(HttpMethod.Get, $"{edit}/listings", null, cancellationToken);
        var existing = JsonRead.Items(current.RootElement, "listings").Select(l => JsonRead.Str(l, "language")).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var listing in listings)
        {
            outcome.Add(await ContentSteps.RunAsync(context, "listing.write", listing.Key, async () =>
            {
                var body = new JsonObject();

                foreach (var field in listing)
                    body[field.Field] = field.Value;

                // PATCH needs an existing listing; PUT creates one.
                if (existing.Contains(listing.Key))
                {
                    using var _ = await api.SendAsync(HttpMethod.Patch, $"{edit}/listings/{Uri.EscapeDataString(listing.Key)}", body, cancellationToken);
                    return $"{listing.Key} listing updated.";
                }

                body["language"] = listing.Key;

                using var created = await api.SendAsync(HttpMethod.Put, $"{edit}/listings/{Uri.EscapeDataString(listing.Key)}", body, cancellationToken);
                return $"{listing.Key} listing created.";
            }, cancellationToken));
        }
    }

    /// <summary>
    /// Replaces every image of one type when the ordered SHA-256 list differs (Play has no reorder: delete all, upload in
    /// order); an unchanged list is skipped.
    /// </summary>
    private async Task<string> ReplaceImagesAsync(GooglePlayApi api, string edit, MediaSetChange set, CancellationToken cancellationToken)
    {
        var path = $"{edit}/listings/{Uri.EscapeDataString(set.Locale)}/{set.ClassKey}";

        using (var current = await api.SendAsync(HttpMethod.Get, path, null, cancellationToken))
        {
            var currentChecksums = JsonRead.Items(current.RootElement, "images").Select(i => JsonRead.Str(i, "sha256")?.ToLowerInvariant()).ToList();

            if (currentChecksums.SequenceEqual(set.Items.Select(i => i.Sha256?.ToLowerInvariant())))
                return $"{set.Locale} {set.ClassKey}: unchanged.";
        }

        using (var _ = await api.SendAsync(HttpMethod.Delete, path, null, cancellationToken))
        {
            // Every image of the type is removed; the desired ones are uploaded in order below.
        }

        foreach (var item in set.Items)
        {
            var data = await ContentSteps.ReadObjectAsync(_fileStorage, item.ObjectKey, cancellationToken);

            using var _ = await api.UploadMediaAsync($"{path}", data, item.ContentType, cancellationToken);
        }

        return $"{set.Locale} {set.ClassKey}: {set.Items.Count} images uploaded.";
    }

    private async Task<string> UploadBundleAsync(GooglePlayApi api, string edit, BundleChange bundle, IStoreJobContext context, CancellationToken cancellationToken)
    {
        var step = await context.BeginStepAsync("bundle.upload", StepDirection.Outbound, bundle.FileName, bundle.Size, cancellationToken);

        try
        {
            await using var stream = await _fileStorage.OpenReadAsync(bundle.ObjectKey, cancellationToken);
            using var result = await api.UploadResumableAsync($"{edit}/bundles", stream, bundle.Size, sent => context.ReportProgressAsync(step, sent, cancellationToken), cancellationToken);

            var versionCode = JsonRead.Str(result.RootElement, "versionCode") ?? throw new StoreCallException("Play answered the bundle upload without a version code.", null, false);

            await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, $"versionCode {versionCode}", cancellationToken);

            return versionCode;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await context.EndStepAsync(step, StoreJobStepStatus.Failed, ex.Message, cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Writes a release to a track. A staged (inProgress) release keeps the track's completed release next to it, so the
    /// users outside the rollout keep the previous version; any other release replaces the track's releases.
    /// </summary>
    private static async Task<string> WriteReleaseAsync(GooglePlayApi api, string edit, TrackReleaseChange release, string uploadedVersionCode, CancellationToken cancellationToken)
    {
        var codes = release.VersionCodes.Count > 0 ? release.VersionCodes : uploadedVersionCode == null ? [] : [uploadedVersionCode];

        if (codes.Count == 0)
            throw new StoreCallException("A release needs at least one version code.", null, false);

        var releases = new JsonArray(ReleaseNode(new TrackReleaseState
        {
            Name = release.Name,
            VersionCodes = [.. codes],
            Status = release.Status ?? "completed",
            UserFraction = release.Status == "inProgress" ? release.UserFraction : null,
            ReleaseNotes = release.ReleaseNotes,
            Countries = release.Countries,
            IncludeRestOfWorld = release.IncludeRestOfWorld,
        }));

        if (release.Status == "inProgress")
        {
            using var current = await api.SendAsync(HttpMethod.Get, $"{edit}/tracks/{Uri.EscapeDataString(release.Track)}", null, cancellationToken);
            var completed = JsonRead.Items(current.RootElement, "releases").Select(ParseRelease).FirstOrDefault(r => r.Status == "completed");

            if (completed != null)
                releases.Add(ReleaseNode(completed));
        }

        using var _ = await api.SendAsync(HttpMethod.Put, $"{edit}/tracks/{Uri.EscapeDataString(release.Track)}", new JsonObject { ["track"] = release.Track, ["releases"] = releases }, cancellationToken);

        return $"{release.Track}: {string.Join(", ", codes)} ({release.Status ?? "completed"}).";
    }

    /// <summary>
    /// Changes the staged rollout of a track: the fraction only grows, halt stops it, resume continues it, complete releases
    /// to everyone and drops the previous completed release.
    /// </summary>
    private static async Task<string> WriteRolloutAsync(GooglePlayApi api, string edit, RolloutChange rollout, CancellationToken cancellationToken)
    {
        var track = rollout.Track ?? ChangePlanRules.ProductionTrack;

        using var current = await api.SendAsync(HttpMethod.Get, $"{edit}/tracks/{Uri.EscapeDataString(track)}", null, cancellationToken);
        var releases = JsonRead.Items(current.RootElement, "releases").Select(ParseRelease).ToList();
        var staged = releases.FirstOrDefault(r => r.Status is "inProgress" or "halted") ?? throw new StoreCallException($"Track {track} has no staged rollout.", null, false);

        switch (rollout.Action)
        {
            case RolloutAction.SetPercentage:
                staged.Status = "inProgress";
                staged.UserFraction = rollout.UserFraction;
                break;
            case RolloutAction.Halt:
                staged.Status = "halted";
                break;
            case RolloutAction.Resume:
                staged.Status = "inProgress";
                break;
            case RolloutAction.Complete:
                staged.Status = "completed";
                staged.UserFraction = null;
                releases = [staged];
                break;
            default:
                throw new StoreCallException($"{rollout.Action} is not a Play rollout action.", null, false);
        }

        var body = new JsonObject { ["track"] = track, ["releases"] = new JsonArray([.. releases.Select(r => (JsonNode)ReleaseNode(r))]) };

        using var _ = await api.SendAsync(HttpMethod.Put, $"{edit}/tracks/{Uri.EscapeDataString(track)}", body, cancellationToken);

        return $"{track}: {staged.Status}{(staged.UserFraction is { } fraction ? $" {fraction:P0}" : string.Empty)}.";
    }

    private static JsonObject ReleaseNode(TrackReleaseState release)
    {
        var node = new JsonObject
        {
            ["versionCodes"] = new JsonArray([.. release.VersionCodes.Select(c => (JsonNode)c)]),
            ["status"] = release.Status,
        };

        if (release.Name != null)
            node["name"] = release.Name;

        if (release.UserFraction != null && release.Status is "inProgress" or "halted")
            node["userFraction"] = release.UserFraction;

        if (release.ReleaseNotes?.Count > 0)
            node["releaseNotes"] = JsonSerializer.SerializeToNode(release.ReleaseNotes.Select(n => new { language = n.Language, text = n.Text }), _json);

        if (release.Countries != null)
            node["countryTargeting"] = new JsonObject { ["countries"] = new JsonArray([.. release.Countries.Select(c => (JsonNode)c)]), ["includeRestOfWorld"] = release.IncludeRestOfWorld ?? false };

        return node;
    }
}
