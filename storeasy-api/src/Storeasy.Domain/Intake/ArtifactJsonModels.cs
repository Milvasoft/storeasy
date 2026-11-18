using Storeasy.Domain.Enums;

namespace Storeasy.Domain.Intake;

/// <summary>
/// CI details of a package's source. Every field is optional.
/// </summary>
public sealed class ArtifactSourceRef
{
    /// <summary>
    /// Provider name (github, azure-devops, gitlab, eas, codemagic...).
    /// </summary>
    public string Provider { get; set; }

    /// <summary>
    /// Run id.
    /// </summary>
    public string RunId { get; set; }

    /// <summary>
    /// Run attempt.
    /// </summary>
    public string RunAttempt { get; set; }

    /// <summary>
    /// Link to the run.
    /// </summary>
    public string RunUrl { get; set; }

    /// <summary>
    /// Repository.
    /// </summary>
    public string Repository { get; set; }

    /// <summary>
    /// Branch.
    /// </summary>
    public string Branch { get; set; }

    /// <summary>
    /// Commit.
    /// </summary>
    public string Commit { get; set; }

    /// <summary>
    /// Who started the run.
    /// </summary>
    public string Actor { get; set; }
}

/// <summary>
/// A delivery target given for one session, overriding the app's delivery rule (tools/CONTRACT.md 3.2).
/// </summary>
public sealed class ArtifactDeliveryOverride
{
    /// <summary>
    /// Store; must match the package's platform.
    /// </summary>
    public StoreKind? Store { get; set; }

    /// <summary>
    /// Play testing track.
    /// </summary>
    public string Track { get; set; }

    /// <summary>
    /// Internal TestFlight group name.
    /// </summary>
    public string Group { get; set; }

    /// <summary>
    /// Release notes or "What to Test" text.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Read and check the package, then hold it until a member confirms the delivery (<c>artifacts/session/deliver</c>);
    /// the web app's manual upload sets it so the checks are shown before anything reaches a store.
    /// </summary>
    public bool HoldForConfirmation { get; set; }
}

/// <summary>
/// One processing step of a session.
/// </summary>
public sealed class ArtifactStep
{
    /// <summary>
    /// Step.
    /// </summary>
    public ArtifactStepKind Kind { get; set; }

    /// <summary>
    /// State.
    /// </summary>
    public ArtifactStepStatus Status { get; set; } = ArtifactStepStatus.Pending;

    /// <summary>
    /// Short note.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Bytes done (upload, delivery).
    /// </summary>
    public long? Bytes { get; set; }

    /// <summary>
    /// Bytes in total.
    /// </summary>
    public long? Total { get; set; }

    /// <summary>
    /// Resumes of the upload.
    /// </summary>
    public int? ResumeCount { get; set; }

    /// <summary>
    /// UTC start.
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// UTC end.
    /// </summary>
    public DateTime? EndDate { get; set; }
}

/// <summary>
/// What was read from a package.
/// </summary>
public sealed class ArtifactPackageInfo
{
    /// <summary>
    /// Format.
    /// </summary>
    public PackageKind Kind { get; set; }

    /// <summary>
    /// Bundle id, package name or package identity name.
    /// </summary>
    public string Identifier { get; set; }

    /// <summary>
    /// User facing version (CFBundleShortVersionString, versionName, MSIX version).
    /// </summary>
    public string VersionName { get; set; }

    /// <summary>
    /// Build number (CFBundleVersion, versionCode).
    /// </summary>
    public string VersionCode { get; set; }

    /// <summary>
    /// Minimum OS (MinimumOSVersion, minSdkVersion, MinVersion).
    /// </summary>
    public string MinOs { get; set; }

    /// <summary>
    /// Android target SDK.
    /// </summary>
    public string TargetSdk { get; set; }

    /// <summary>
    /// Signature summary when it could be read.
    /// </summary>
    public string Signature { get; set; }

    /// <summary>
    /// Xcode version that built an IPA (e.g. 26.1).
    /// </summary>
    public string XcodeVersion { get; set; }

    /// <summary>
    /// Platform SDK version an IPA was built with (e.g. 26.1).
    /// </summary>
    public string SdkVersion { get; set; }

    /// <summary>
    /// Apple platform of an IPA (IOS, TV_OS, VISION_OS) for the App Store Connect upload.
    /// </summary>
    public string ApplePlatform { get; set; }

    /// <summary>
    /// Whether the IPA declares ITSAppUsesNonExemptEncryption (null when it does not say).
    /// </summary>
    public bool? UsesNonExemptEncryption { get; set; }

    /// <summary>
    /// MSIX publisher.
    /// </summary>
    public string Publisher { get; set; }

    /// <summary>
    /// Display name, when the package has one.
    /// </summary>
    public string DisplayName { get; set; }
}

/// <summary>
/// Result of one catalog check of a package (same shape as a readiness item).
/// </summary>
public sealed class ArtifactCheck
{
    /// <summary>
    /// Stable key.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Group.
    /// </summary>
    public string Group { get; set; }

    /// <summary>
    /// Outcome: Ok, Missing (blocks) or Warning.
    /// </summary>
    public ReadinessStatus Status { get; set; }

    /// <summary>
    /// Catalog rule, when the check comes from one.
    /// </summary>
    public string RuleId { get; set; }

    /// <summary>
    /// Title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Detail.
    /// </summary>
    public string Message { get; set; }
}

/// <summary>
/// The delivery target applied to a session.
/// </summary>
public sealed class ArtifactAppliedRule
{
    /// <summary>
    /// Platform.
    /// </summary>
    public ArtifactPlatform Platform { get; set; }

    /// <summary>
    /// Target kind.
    /// </summary>
    public DeliveryTarget Target { get; set; }

    /// <summary>
    /// Target id.
    /// </summary>
    public string TargetId { get; set; }

    /// <summary>
    /// Target name.
    /// </summary>
    public string TargetName { get; set; }

    /// <summary>
    /// Staged rollout fraction of a Play release.
    /// </summary>
    public double? RolloutFraction { get; set; }

    /// <summary>
    /// Release notes.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// What happens on a warning.
    /// </summary>
    public DeliveryWarningAction OnWarning { get; set; }

    /// <summary>
    /// Whether the session's own delivery override was used instead of the rule.
    /// </summary>
    public bool Override { get; set; }

    /// <summary>
    /// Who changed the rule last.
    /// </summary>
    public string RuleUpdatedByUserName { get; set; }

    /// <summary>
    /// When the rule was changed last.
    /// </summary>
    public DateTime? RuleUpdatedDate { get; set; }
}

/// <summary>
/// One claim of an OIDC token (kept from the last exchange of a trusted publisher rule; never the token itself).
/// </summary>
public sealed class OidcClaim
{
    /// <summary>
    /// Claim name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Claim value.
    /// </summary>
    public string Value { get; set; }
}
