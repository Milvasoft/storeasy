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
    /// Renders a mockup template into every selected screenshot class and locale (studio export, phase 6). Numbered in the
    /// studio's own band.
    /// </summary>
    [Description("Mockup export")]
    MockupExport = 60,
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
}
