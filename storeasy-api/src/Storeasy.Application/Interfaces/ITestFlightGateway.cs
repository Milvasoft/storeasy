namespace Storeasy.Application.Interfaces;

/// <summary>
/// App Store Connect TestFlight: beta groups, testers, builds' beta details, beta app localizations and beta app review.
/// Failures throw <see cref="StoreCallException"/>; tests use a fake (no real beta review submission ever).
/// </summary>
public interface ITestFlightGateway
{
    /// <summary>
    /// Reads the TestFlight state of one app.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="appExternalId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<TestFlightSnapshot> ReadAsync(string secretJson, string appExternalId, CancellationToken cancellationToken);

    /// <summary>
    /// Writes one change. Returns the store's id of what it created, if anything.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="appExternalId"></param>
    /// <param name="change"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<string> ApplyAsync(string secretJson, string appExternalId, TestFlightChange change, CancellationToken cancellationToken);
}

/// <summary>
/// The TestFlight state of an app.
/// </summary>
/// <param name="Groups">Beta groups.</param>
/// <param name="Testers">Testers.</param>
/// <param name="Builds">Beta details of the builds.</param>
/// <param name="Localizations">Beta app localizations.</param>
public sealed record TestFlightSnapshot(IReadOnlyList<BetaGroupRecord> Groups,
                                       IReadOnlyList<BetaTesterRecord> Testers,
                                       IReadOnlyList<BetaBuildRecord> Builds,
                                       IReadOnlyList<BetaLocalizationRecord> Localizations);

/// <summary>
/// A beta group.
/// </summary>
/// <param name="ExternalId">Group id.</param>
/// <param name="Name">Name.</param>
/// <param name="IsInternal">Internal group.</param>
/// <param name="PublicLinkEnabled">Public link on.</param>
/// <param name="PublicLink">Public link.</param>
/// <param name="PublicLinkLimit">Public link limit.</param>
/// <param name="PublicLinkLimitEnabled">Limit on.</param>
/// <param name="BuildExternalIds">Builds in the group.</param>
public sealed record BetaGroupRecord(string ExternalId, string Name, bool IsInternal, bool PublicLinkEnabled, string PublicLink, int? PublicLinkLimit, bool PublicLinkLimitEnabled, IReadOnlyList<string> BuildExternalIds);

/// <summary>
/// A tester.
/// </summary>
/// <param name="ExternalId">Tester id.</param>
/// <param name="FirstName">First name.</param>
/// <param name="LastName">Last name.</param>
/// <param name="Email">E-mail.</param>
/// <param name="InviteType">How the tester joined.</param>
/// <param name="State">State.</param>
/// <param name="GroupExternalIds">Groups of the tester.</param>
public sealed record BetaTesterRecord(string ExternalId, string FirstName, string LastName, string Email, BetaInviteType InviteType, BetaTesterState State, IReadOnlyList<string> GroupExternalIds);

/// <summary>
/// Beta details of a build.
/// </summary>
/// <param name="BuildExternalId">Build id.</param>
/// <param name="ReviewState">Beta app review state.</param>
/// <param name="WhatToTest">"What to test" per locale.</param>
/// <param name="ExpirationDate">UTC instant the build stops being testable.</param>
/// <param name="Expired">Whether it expired.</param>
public sealed record BetaBuildRecord(string BuildExternalId, BetaReviewState ReviewState, IReadOnlyList<LocalizedText> WhatToTest, DateTime? ExpirationDate, bool Expired);

/// <summary>
/// A beta app localization.
/// </summary>
/// <param name="ExternalId">Localization id.</param>
/// <param name="Locale">Locale.</param>
/// <param name="Description">Beta app description.</param>
/// <param name="FeedbackEmail">Feedback e-mail.</param>
/// <param name="MarketingUrl">Marketing URL.</param>
/// <param name="PrivacyPolicyUrl">Privacy policy URL.</param>
public sealed record BetaLocalizationRecord(string ExternalId, string Locale, string Description, string FeedbackEmail, string MarketingUrl, string PrivacyPolicyUrl);

/// <summary>
/// One TestFlight write. Only the fields of <see cref="Operation"/> are read.
/// </summary>
public sealed record TestFlightChange
{
    /// <summary>What to do.</summary>
    public TestFlightOperation Operation { get; init; }

    /// <summary>Group (update, testers, builds).</summary>
    public string GroupExternalId { get; init; }

    /// <summary>Group name (create, update).</summary>
    public string Name { get; init; }

    /// <summary>Internal group (create).</summary>
    public bool IsInternal { get; init; }

    /// <summary>Public link on (create, update).</summary>
    public bool? PublicLinkEnabled { get; init; }

    /// <summary>Public link limit (create, update).</summary>
    public int? PublicLinkLimit { get; init; }

    /// <summary>Limit on (create, update).</summary>
    public bool? PublicLinkLimitEnabled { get; init; }

    /// <summary>Testers to add (by e-mail).</summary>
    public IReadOnlyList<BetaTesterInvite> AddTesters { get; init; } = [];

    /// <summary>Testers to remove (store ids).</summary>
    public IReadOnlyList<string> RemoveTesterExternalIds { get; init; } = [];

    /// <summary>Builds to add (store ids).</summary>
    public IReadOnlyList<string> AddBuildExternalIds { get; init; } = [];

    /// <summary>Builds to remove (store ids).</summary>
    public IReadOnlyList<string> RemoveBuildExternalIds { get; init; } = [];

    /// <summary>Build (what to test, beta review).</summary>
    public string BuildExternalId { get; init; }

    /// <summary>Locale (what to test, localization).</summary>
    public string Locale { get; init; }

    /// <summary>"What to test" text, or the beta app description.</summary>
    public string Text { get; init; }

    /// <summary>Feedback e-mail (localization).</summary>
    public string FeedbackEmail { get; init; }

    /// <summary>Marketing URL (localization).</summary>
    public string MarketingUrl { get; init; }

    /// <summary>Privacy policy URL (localization).</summary>
    public string PrivacyPolicyUrl { get; init; }
}

/// <summary>
/// A tester to invite.
/// </summary>
/// <param name="Email">E-mail.</param>
/// <param name="FirstName">First name.</param>
/// <param name="LastName">Last name.</param>
public sealed record BetaTesterInvite(string Email, string FirstName, string LastName);
