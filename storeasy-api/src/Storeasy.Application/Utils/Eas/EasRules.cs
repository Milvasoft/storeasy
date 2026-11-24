using Storeasy.Application.Utils.Constants;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Storeasy.Application.Utils.Eas;

/// <summary>
/// Rules of EAS builds: the Expo state names, which states are still running, the typed confirmation of a start, git refs
/// and profiles storeasy passes on, the build profiles of an eas.json and the address of a build on expo.dev.
/// </summary>
public static partial class EasRules
{
    /// <summary>
    /// Build profiles offered when eas.json cannot be read (the ones <c>eas build:configure</c> writes).
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultProfiles = ["development", "preview", "production"];

    [GeneratedRegex(@"^[A-Za-z0-9._/-]{1,200}$")]
    private static partial Regex GitRefPattern();

    [GeneratedRegex(@"^[A-Za-z0-9._-]{1,100}$")]
    private static partial Regex ProfilePattern();

    /// <summary>
    /// Associated data of the sealed Expo token (binds it to its organization).
    /// </summary>
    public static string Context(int organizationId) => string.Create(CultureInfo.InvariantCulture, $"ExpoConnection:{organizationId}");

    /// <summary>
    /// The state of Expo's <c>BuildStatus</c> name (<c>IN_QUEUE</c>, <c>FINISHED</c>...).
    /// </summary>
    public static EasBuildStatus StatusOf(string status) => status switch
    {
        "NEW" => EasBuildStatus.New,
        "IN_QUEUE" => EasBuildStatus.InQueue,
        "IN_PROGRESS" => EasBuildStatus.InProgress,
        "PENDING_CANCEL" => EasBuildStatus.PendingCancel,
        "FINISHED" => EasBuildStatus.Finished,
        "ERRORED" => EasBuildStatus.Errored,
        "CANCELED" => EasBuildStatus.Canceled,
        _ => EasBuildStatus.New,
    };

    /// <summary>
    /// The platform of Expo's <c>AppPlatform</c> name.
    /// </summary>
    public static EasPlatform PlatformOf(string platform) => platform == "IOS" ? EasPlatform.Ios : EasPlatform.Android;

    /// <summary>
    /// Expo's <c>AppPlatform</c> name of a single platform.
    /// </summary>
    public static string ExpoPlatform(EasPlatform platform) => platform == EasPlatform.Ios ? "IOS" : "ANDROID";

    /// <summary>
    /// The platforms one start makes builds for.
    /// </summary>
    public static IReadOnlyList<EasPlatform> Expand(EasPlatform platform) => platform == EasPlatform.All ? [EasPlatform.Ios, EasPlatform.Android] : [platform];

    /// <summary>
    /// Whether a build is still running (or not created yet), so the schedule follows it.
    /// </summary>
    public static bool IsActive(EasBuildStatus status) => status is EasBuildStatus.Requested or EasBuildStatus.New or EasBuildStatus.InQueue or EasBuildStatus.InProgress or EasBuildStatus.PendingCancel;

    /// <summary>
    /// Whether a build can still be canceled.
    /// </summary>
    public static bool IsCancelable(EasBuildStatus status) => status is EasBuildStatus.New or EasBuildStatus.InQueue or EasBuildStatus.InProgress;

    /// <summary>
    /// The word of a platform in the typed confirmation: <c>iOS</c>, <c>Android</c> or <c>all</c>.
    /// </summary>
    public static string PlatformWord(EasPlatform platform) => platform switch
    {
        EasPlatform.Ios => "iOS",
        EasPlatform.Android => "Android",
        _ => "all",
    };

    /// <summary>
    /// What a member types to start a build: the app name and the platform word (<c>Pusula iOS</c>).
    /// </summary>
    public static string ConfirmText(string appName, EasPlatform platform) => $"{appName} {PlatformWord(platform)}";

    /// <summary>
    /// Whether the typed text is the confirmation (exact, surrounding spaces ignored).
    /// </summary>
    public static bool ConfirmationMatches(string expected, string typed) => typed != null && string.Equals(expected.Trim(), typed.Trim(), StringComparison.Ordinal);

    /// <summary>
    /// Whether <paramref name="gitRef"/> is a branch, tag or commit name storeasy passes to Expo.
    /// </summary>
    public static bool IsGitRef(string gitRef) => gitRef != null && GitRefPattern().IsMatch(gitRef) && !gitRef.Contains("..", StringComparison.Ordinal) && !gitRef.StartsWith('-') && !gitRef.EndsWith('/');

    /// <summary>
    /// Whether <paramref name="profile"/> is an eas.json build profile name.
    /// </summary>
    public static bool IsProfile(string profile) => profile != null && ProfilePattern().IsMatch(profile);

    /// <summary>
    /// The build profile names of an eas.json (the keys of <c>build</c>); null when the text is not an eas.json.
    /// </summary>
    public static List<string> ProfilesOf(string easJson)
    {
        try
        {
            using var document = JsonDocument.Parse(easJson ?? string.Empty, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("build", out var build) || build.ValueKind != JsonValueKind.Object)
                return null;

            return [.. build.EnumerateObject().Select(p => p.Name).Where(IsProfile)];
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The build's page on expo.dev.
    /// </summary>
    public static string BuildPageUrl(string websiteUrl, string accountName, string slug, string buildId)
        => accountName == null || slug == null || buildId == null ? null : $"{websiteUrl.TrimEnd('/')}/accounts/{Uri.EscapeDataString(accountName)}/projects/{Uri.EscapeDataString(slug)}/builds/{Uri.EscapeDataString(buildId)}";

    /// <summary>
    /// The project's GitHub settings on expo.dev, where the repository is connected.
    /// </summary>
    public static string GitHubSettingsUrl(string websiteUrl, string accountName, string slug)
        => $"{websiteUrl.TrimEnd('/')}/accounts/{Uri.EscapeDataString(accountName)}/projects/{Uri.EscapeDataString(slug)}/github";

    /// <summary>
    /// The extension of a build package address (<c>.ipa</c>, <c>.aab</c>, <c>.apk</c>), or null for anything else (for
    /// example a simulator <c>.tar.gz</c>).
    /// </summary>
    public static string PackageExtension(string artifactUrl)
    {
        if (!Uri.TryCreate(artifactUrl, UriKind.Absolute, out var uri))
            return null;

        var extension = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();

        return extension is ".ipa" or ".aab" or ".apk" ? extension : null;
    }

    /// <summary>
    /// How well an Expo project matches a storeasy app: 3 for a bundle id or package name of the app's store records, 2 for
    /// the slug or name, 0 for none.
    /// </summary>
    public static int MatchScore(string appName, IEnumerable<string> appIdentifiers, string projectSlug, string projectName, string projectIdentifier)
    {
        if (projectIdentifier != null && appIdentifiers.Any(i => string.Equals(i, projectIdentifier, StringComparison.OrdinalIgnoreCase)))
            return 3;

        var name = Normalize(appName);

        return name.Length > 0 && (name == Normalize(projectSlug) || name == Normalize(projectName)) ? 2 : 0;
    }

    /// <summary>
    /// Play tracks EAS Submit takes (the values eas.json's <c>track</c> uses).
    /// </summary>
    public static readonly IReadOnlyList<string> Tracks = ["internal", "alpha", "beta", "production"];

    /// <summary>
    /// Play release statuses of EAS Submit (Expo's <c>SubmissionAndroidReleaseStatus</c>).
    /// </summary>
    public static readonly IReadOnlyList<string> ReleaseStatuses = ["COMPLETED", "DRAFT", "IN_PROGRESS", "HALTED"];

    /// <summary>
    /// The state of Expo's <c>SubmissionStatus</c> name.
    /// </summary>
    public static EasSubmissionStatus SubmissionStatusOf(string status) => status switch
    {
        "AWAITING_BUILD" => EasSubmissionStatus.AwaitingBuild,
        "IN_PROGRESS" => EasSubmissionStatus.InProgress,
        "FINISHED" => EasSubmissionStatus.Finished,
        "ERRORED" => EasSubmissionStatus.Errored,
        "CANCELED" => EasSubmissionStatus.Canceled,
        _ => EasSubmissionStatus.InQueue,
    };

    /// <summary>
    /// Whether a submission is still running.
    /// </summary>
    public static bool IsActive(EasSubmissionStatus status) => status is EasSubmissionStatus.AwaitingBuild or EasSubmissionStatus.InQueue or EasSubmissionStatus.InProgress;

    /// <summary>
    /// The message key of after build options that cannot be used; null when they can.
    /// </summary>
    public static string ChainProblem(EasAfterBuild afterBuild, string track, string releaseStatus)
    {
        if (!Enum.IsDefined(afterBuild))
            return MessageKey.EasChainInvalid;

        return afterBuild == EasAfterBuild.Submit ? SubmitProblem(track ?? "internal", releaseStatus ?? "COMPLETED") : null;
    }

    /// <summary>
    /// The message key of a Play track or release status EAS Submit does not take; null when both are fine.
    /// </summary>
    public static string SubmitProblem(string track, string releaseStatus)
        => Tracks.Contains(track) && ReleaseStatuses.Contains(releaseStatus) ? null : MessageKey.EasSubmitTargetInvalid;

    /// <summary>
    /// Whether an EAS Submit to <paramref name="track"/> on <paramref name="platform"/> is a production release (it needs the
    /// production release permission).
    /// </summary>
    public static bool IsProductionSubmit(EasPlatform platform, string track) => platform != EasPlatform.Ios && track == "production";

    private static string Normalize(string text) => new([.. (text ?? string.Empty).ToLowerInvariant().Where(char.IsLetterOrDigit)]);

    /// <summary>
    /// The message key of a start refused before Expo is called; null when the values can be sent.
    /// </summary>
    public static string StartProblem(EasPlatform platform, string profile, string gitRef)
    {
        if (!Enum.IsDefined(platform))
            return MessageKey.EasPlatformInvalid;

        if (!IsProfile(profile))
            return MessageKey.EasProfileInvalid;

        return IsGitRef(gitRef) ? null : MessageKey.EasGitRefInvalid;
    }
}
