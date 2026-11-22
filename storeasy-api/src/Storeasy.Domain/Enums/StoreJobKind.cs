using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// What a store job does. Each kind has one handler in the store engine. Values are explicit and never reused.
/// </summary>
public enum StoreJobKind : short
{
    /// <summary>
    /// Pulls the apps and builds of one store connection and links them to storeasy apps.
    /// </summary>
    [Description("Sync apps and builds")]
    SyncApps = 1,

    /// <summary>
    /// Applies an approved change plan to a store.
    /// </summary>
    [Description("Apply a change plan")]
    ApplyChangePlan = 2,

    /// <summary>
    /// Reads the subscriptions and In-App Purchases of a store record into storeasy.
    /// </summary>
    [Description("Sync store products")]
    SyncMonetization = 70,

    /// <summary>
    /// Reads the App Store experiments and custom product pages of a store record into storeasy.
    /// </summary>
    [Description("Sync store listing experiments")]
    SyncAsoExperiments = 80,

    /// <summary>
    /// Renders a mockup template into every selected screenshot class and locale (studio export, phase 6). Numbered in the
    /// studio's own band.
    /// </summary>
    [Description("Mockup export")]
    MockupExport = 60,

    /// <summary>
    /// Reviews one store record with AI the way that store's reviewer would (pre-review). Reads the store and storeasy's
    /// records, asks Claude with the organization's key and saves the report; nothing is written to a store.
    /// </summary>
    [Description("AI pre-review")]
    PreReview = 90,

    /// Processes one inbound webhook event (App Store version state change: state, rejection case, notifications).
    /// </summary>
    [Description("Process webhook event")]
    ProcessWebhookEvent = 400,

    /// <summary>
    /// Registers, updates or removes the App Store Connect webhooks of an inbound endpoint.
    /// </summary>
    [Description("Register webhooks")]
    RegisterWebhooks = 401,

    /// <summary>
    /// Asks App Store Connect to send a test event (ping) to every webhook of an inbound endpoint.
    /// </summary>
    [Description("Ping webhooks")]
    PingWebhooks = 407,

    /// <summary>
    /// Reads the App Store version states of a connection's apps (catches what a missed webhook did not report).
    /// </summary>
    [Description("Sync version states")]
    SyncVersionStates = 402,

    /// <summary>
    /// Reads the customer reviews of one store record.
    /// </summary>
    [Description("Sync reviews")]
    SyncReviews = 403,

    /// <summary>
    /// Publishes (or replaces) the developer reply of one review.
    /// </summary>
    [Description("Reply to review")]
    ReplyToReview = 404,

    /// <summary>
    /// Reads the TestFlight groups, testers, builds and beta texts of one store record.
    /// </summary>
    [Description("Sync TestFlight")]
    SyncTestFlight = 405,

    /// <summary>
    /// Writes one TestFlight change (<see cref="TestFlightOperation"/>) and reads the state back.
    /// </summary>
    [Description("Update TestFlight")]
    UpdateTestFlight = 406,

    /// <summary>
    /// Reads the Microsoft Store products of one connection: MSIX apps, MSI/EXE products, submissions and their status,
    /// package flights, add-ons and installer packages.
    /// </summary>
    [Description("Sync Microsoft Store")]
    MsSync = 50,


    /// <summary>
    /// Commits a Microsoft Store submission (sends it to certification) and polls its status.
    /// </summary>
    [Description("Submit to Microsoft Store")]
    MsSubmit = 52,

    /// <summary>
    /// Downloads MSI/EXE installers and checks their Authenticode signature.
    /// </summary>
    [Description("Check Microsoft Store installers")]
    MsCheckInstaller = 53,

    /// <summary>
    /// Reads the winget packages of a connection from winget-pkgs and the review state of their pull requests.
    /// </summary>
    [Description("Sync winget")]
    WingetSync = 130,

    /// <summary>
    /// Reads the Chrome Web Store items of a connection (status of the published and the submitted revision, deploy
    /// percentage, upload state, policy flags, rating summary, weekly users); state changes are audited and notified.
    /// </summary>
    [Description("Chrome Web Store sync")]
    ChromeSync = 140,

    /// <summary>
    /// Reads the store page performance metrics (impressions, page views, downloads) of one store record into storeasy.
    /// Numbered in the analytics band.
    /// </summary>
    [Description("Sync store metrics")]
    SyncStoreMetrics = 120,

    /// <summary>
    /// Asks App Store Connect for the ONGOING analytics reports of one app (a one time write a member confirmed).
    /// </summary>
    [Description("Enable App Store analytics reports")]
    EnableAppleAnalytics = 121,

    /// <summary>
    /// Measures up to 20 search keywords of one store record from the stores' public data (ASO keyword research) and keeps
    /// the measurements as the job's result. Store requests are paced, so it runs as a job. Numbered in the ASO keywords band.
    /// </summary>
    [Description("ASO keyword research")]
    AsoKeywordResearch = 110,

    /// <summary>Reads, checks and delivers an uploaded package (build intake).</summary>
    [Description("Process a package")]
    ProcessArtifact = 30,
}
