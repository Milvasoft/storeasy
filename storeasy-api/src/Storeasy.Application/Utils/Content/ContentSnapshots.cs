using Storeasy.Domain.Content;
using System.Text.Json;

namespace Storeasy.Application.Utils.Content;

/// <summary>
/// Helpers over <see cref="StoreContentSnapshot"/> shared by the planners, the approval conflict check and the readiness
/// check.
/// </summary>
public static class ContentSnapshots
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Every value of the snapshot as content keys: the plain values plus one key per media set (ordered item checksums),
    /// per Play track (its releases) and per Play testers list, so a plan on any of them can be fingerprinted.
    /// </summary>
    /// <param name="snapshot"></param>
    /// <returns></returns>
    public static Dictionary<string, string> Flatten(StoreContentSnapshot snapshot)
    {
        var values = new Dictionary<string, string>(snapshot.Values, StringComparer.Ordinal);

        foreach (var set in snapshot.Media)
            values[MediaKey(set.Kind, set.Locale, set.ClassKey)] = string.Join(",", set.Items.Select(i => i.Checksum ?? i.Id));

        foreach (var track in snapshot.Tracks)
        {
            values[ContentKey.Of(ContentGroups.Track, null, track.Track)] = JsonSerializer.Serialize(track.Releases, _json);

            if (track.GoogleGroups != null)
                values[ContentKey.Of(ContentGroups.Testers, null, track.Track)] = string.Join(",", track.GoogleGroups.Order(StringComparer.OrdinalIgnoreCase));
        }

        if (snapshot.Build != null)
            values[ContentKey.Of(ContentGroups.Build, null, "build")] = snapshot.Build.Id;

        return values;
    }

    /// <summary>
    /// Content key of a media set.
    /// </summary>
    /// <param name="kind"></param>
    /// <param name="locale"></param>
    /// <param name="classKey"></param>
    /// <returns></returns>
    public static string MediaKey(MediaKind kind, string locale, string classKey) => ContentKey.Of(ContentGroups.Media, locale, $"{kind}:{classKey}");

    /// <summary>
    /// What to read to plan (and to check again on approval) a plan of <paramref name="kind"/>.
    /// </summary>
    /// <param name="kind"></param>
    /// <returns></returns>
    public static ContentParts PartsOf(ChangePlanKind kind) => kind switch
    {
        ChangePlanKind.Listing => ContentParts.Texts,
        ChangePlanKind.Details => ContentParts.Details,
        ChangePlanKind.Categories => ContentParts.Categories,
        ChangePlanKind.AgeRating => ContentParts.AgeRating,
        ChangePlanKind.Pricing => ContentParts.Pricing,
        ChangePlanKind.Screenshots => ContentParts.Screenshots,
        ChangePlanKind.Previews => ContentParts.Previews,
        ChangePlanKind.ReviewInfo => ContentParts.Review,
        ChangePlanKind.BuildLink or ChangePlanKind.ExportCompliance or ChangePlanKind.ReleaseSettings or ChangePlanKind.SubmitForReview => ContentParts.Release,
        ChangePlanKind.TrackRelease or ChangePlanKind.Rollout or ChangePlanKind.BundleUpload => ContentParts.Release,
        ChangePlanKind.Testers => ContentParts.Testers,
        _ => ContentParts.None,
    };

    /// <summary>
    /// Short form of a value for a plan item (long texts are cut).
    /// </summary>
    /// <param name="value"></param>
    /// <param name="max"></param>
    /// <returns></returns>
    public static string Short(string value, int max = 4000) => value == null || value.Length <= max ? value : value[..max];
}
