using Storeasy.Domain.Content;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Storeasy.Application.Utils.Firefox;

/// <summary>
/// What a live read of an add-on gives: the add-on detail and its versions.
/// </summary>
/// <param name="Addon">Add-on detail.</param>
/// <param name="Versions">Listed and unlisted versions, newest first.</param>
public sealed record FirefoxAddonState(AmoAddonRead Addon, IReadOnlyList<AmoVersionRead> Versions)
{
    /// <summary>
    /// The newest version of any channel.
    /// </summary>
    public AmoVersionRead Latest => this.Versions.OrderByDescending(v => v.Version, Comparer<string>.Create(FirefoxVersions.Compare)).FirstOrDefault();

    /// <summary>
    /// The newest version of <paramref name="channel"/>.
    /// </summary>
    /// <param name="channel"></param>
    /// <returns></returns>
    public AmoVersionRead LatestOf(FirefoxVersionChannel channel)
        => this.Versions.Where(v => v.Channel == channel).OrderByDescending(v => v.Version, Comparer<string>.Create(FirefoxVersions.Compare)).FirstOrDefault();
}

/// <summary>
/// A Firefox add-on as a store content snapshot (what the shared content read, readiness and the plan conflict check work
/// on): listing values as <c>Listing|locale|Field</c>, the versions with their file status, and keys a plan's approval
/// compares (categories, previews, icon, latest version). The whole read is kept as JSON under <see cref="StateKey"/> so the
/// planner works on the same data.
/// </summary>
public static class FirefoxContent
{
    /// <summary>The live read as JSON.</summary>
    public const string StateKey = "firefox.state";

    /// <summary>Add-on status.</summary>
    public const string StatusKey = "firefox.status";

    /// <summary>Category slugs, comma separated.</summary>
    public const string CategoriesKey = "firefox.categories";

    /// <summary>Preview ids, comma separated, in order.</summary>
    public const string PreviewsKey = "firefox.previews";

    /// <summary>Icon address.</summary>
    public const string IconKey = "firefox.icon";

    /// <summary>Version on the add-on page.</summary>
    public const string ListedVersionKey = "firefox.listed.version";

    /// <summary>Newest version of any channel.</summary>
    public const string LatestVersionKey = "firefox.latest.version";

    /// <summary>File status of the newest version.</summary>
    public const string LatestStatusKey = "firefox.latest.status";

    /// <summary>Prefix of a version's release notes hash.</summary>
    public const string NotesPrefix = "firefox.notes|";

    /// <summary>Platform name of the snapshot.</summary>
    public const string Platform = "firefox";

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The snapshot of a live read.
    /// </summary>
    /// <param name="state"></param>
    /// <returns></returns>
    public static StoreContentSnapshot ToSnapshot(FirefoxAddonState state)
    {
        var addon = state.Addon;
        var latest = state.Latest;
        var snapshot = new StoreContentSnapshot
        {
            Store = StoreKind.Firefox,
            ExternalId = addon.Id.ToString(CultureInfo.InvariantCulture),
            VersionId = latest?.Id.ToString(CultureInfo.InvariantCulture),
            VersionString = latest?.Version,
            VersionState = latest?.FileStatus.ToString(),
            VersionEditable = false,
            HasLiveVersion = addon.Status == FirefoxAddonStatus.Public && addon.CurrentVersion != null,
            Platform = Platform,
            PrimaryLocale = addon.DefaultLocale,
            Versions = [.. state.Versions.Select(v => new StoreVersionState { Id = v.Id.ToString(CultureInfo.InvariantCulture), VersionString = v.Version, Platform = Platform, State = $"{v.Channel} {v.FileStatus}" })],
        };

        foreach (var (field, values) in ListingOf(addon))
        {
            foreach (var (locale, value) in values)
            {
                if (!string.IsNullOrEmpty(value))
                    snapshot.Values[ContentKey.Of(ContentGroups.Listing, locale, field)] = value;
            }
        }

        snapshot.Locales = [.. addon.Name.Keys.Concat(addon.Summary.Keys).Concat(addon.Description.Keys).Where(l => !l.StartsWith('_')).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        snapshot.Values[StatusKey] = addon.Status.ToString();
        snapshot.Values[CategoriesKey] = string.Join(',', addon.Categories.Order(StringComparer.Ordinal));
        snapshot.Values[PreviewsKey] = string.Join(',', addon.Previews.OrderBy(p => p.Position).ThenBy(p => p.Id).Select(p => p.Id.ToString(CultureInfo.InvariantCulture)));
        snapshot.Values[IconKey] = addon.IconUrl ?? string.Empty;
        snapshot.Values[ListedVersionKey] = addon.CurrentVersion?.Version ?? string.Empty;
        snapshot.Values[LatestVersionKey] = latest?.Version ?? string.Empty;
        snapshot.Values[LatestStatusKey] = latest == null ? string.Empty : $"{latest.Channel} {latest.FileStatus}";

        foreach (var version in state.Versions)
            snapshot.Values[NotesPrefix + version.Id.ToString(CultureInfo.InvariantCulture)] = Hash(version.ReleaseNotes);

        snapshot.Values[StateKey] = JsonSerializer.Serialize(state, _json);

        return snapshot;
    }

    /// <summary>
    /// The live read a snapshot was made from; null when it has none.
    /// </summary>
    /// <param name="snapshot"></param>
    /// <returns></returns>
    public static FirefoxAddonState FromSnapshot(StoreContentSnapshot snapshot)
        => snapshot?.Get(StateKey) is { Length: > 0 } json ? JsonSerializer.Deserialize<FirefoxAddonState>(json, _json) : null;

    /// <summary>
    /// The listing values of an add-on, field to (locale to value).
    /// </summary>
    /// <param name="addon"></param>
    /// <returns></returns>
    public static List<(string Field, Dictionary<string, string> Values)> ListingOf(AmoAddonRead addon)
        => [(FirefoxFields.Name, addon.Name), (FirefoxFields.Summary, addon.Summary), (FirefoxFields.Description, addon.Description),
            (FirefoxFields.Homepage, addon.Homepage), (FirefoxFields.SupportUrl, addon.SupportUrl), (FirefoxFields.SupportEmail, addon.SupportEmail)];

    /// <summary>
    /// The values of one listing field.
    /// </summary>
    /// <param name="addon"></param>
    /// <param name="field"></param>
    /// <returns></returns>
    public static Dictionary<string, string> ValuesOf(AmoAddonRead addon, string field)
        => ListingOf(addon).FirstOrDefault(f => f.Field == field).Values ?? new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// A hash of texts per locale, independent of their order.
    /// </summary>
    /// <param name="texts"></param>
    /// <returns></returns>
    public static string Hash(IReadOnlyDictionary<string, string> texts)
    {
        var builder = new StringBuilder();

        foreach (var (locale, text) in (texts ?? new Dictionary<string, string>()).Where(t => !string.IsNullOrEmpty(t.Value)).OrderBy(t => t.Key, StringComparer.Ordinal))
            builder.Append(locale).Append('\n').Append(text.Replace("\r\n", "\n", StringComparison.Ordinal)).Append('\n');

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))[..16];
    }
}
