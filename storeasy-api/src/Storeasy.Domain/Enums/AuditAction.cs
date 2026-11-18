using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Kinds of organization audit entries. Values are explicit and never reused.
/// </summary>
public enum AuditAction : short
{
    /// <summary>Organization created.</summary>
    [Description("Organization created")]
    OrganizationCreated = 1,

    /// <summary>Organization renamed.</summary>
    [Description("Organization updated")]
    OrganizationUpdated = 2,

    /// <summary>Invitation sent.</summary>
    [Description("Member invited")]
    MemberInvited = 10,

    /// <summary>Invitation revoked.</summary>
    [Description("Invitation revoked")]
    InvitationRevoked = 11,

    /// <summary>Invitation accepted.</summary>
    [Description("Member joined")]
    MemberJoined = 12,

    /// <summary>Member role changed.</summary>
    [Description("Member role changed")]
    MemberRoleChanged = 13,

    /// <summary>Member removed or left.</summary>
    [Description("Member removed")]
    MemberRemoved = 14,

    /// <summary>Store connection added.</summary>
    [Description("Store connection added")]
    ConnectionAdded = 20,

    /// <summary>Store connection secret replaced or renamed.</summary>
    [Description("Store connection updated")]
    ConnectionUpdated = 21,

    /// <summary>Store connection removed.</summary>
    [Description("Store connection removed")]
    ConnectionRemoved = 22,

    /// <summary>Store connection verified against the store.</summary>
    [Description("Store connection verified")]
    ConnectionVerified = 23,

    /// <summary>Store job cancelled.</summary>
    [Description("Store job cancelled")]
    JobCancelled = 30,

    /// <summary>A store record was added by hand (a Play app by package name).</summary>
    [Description("App added")]
    AppAdded = 40,

    /// <summary>A store record was moved to another storeasy app.</summary>
    [Description("Store record linked")]
    StoreAppLinked = 41,

    /// <summary>A store record was split off into its own storeasy app.</summary>
    [Description("Store record unlinked")]
    StoreAppUnlinked = 42,

    /// <summary>Inbound webhook endpoint added.</summary>
    [Description("Webhook endpoint added")]
    WebhookEndpointAdded = 400,

    /// <summary>Inbound webhook secret rotated.</summary>
    [Description("Webhook secret rotated")]
    WebhookSecretRotated = 401,

    /// <summary>Inbound webhook endpoint removed.</summary>
    [Description("Webhook endpoint removed")]
    WebhookEndpointRemoved = 402,

    /// <summary>Inbound webhook event processed again.</summary>
    [Description("Webhook event reprocessed")]
    WebhookEventReprocessed = 403,

    /// <summary>Outgoing webhook added.</summary>
    [Description("Outgoing webhook added")]
    OrgWebhookAdded = 410,

    /// <summary>Outgoing webhook changed (address, events, secret, state).</summary>
    [Description("Outgoing webhook updated")]
    OrgWebhookUpdated = 411,

    /// <summary>Outgoing webhook removed.</summary>
    [Description("Outgoing webhook removed")]
    OrgWebhookRemoved = 412,

    /// <summary>Rejection case opened.</summary>
    [Description("Rejection opened")]
    RejectionOpened = 420,

    /// <summary>Rejection text pasted.</summary>
    [Description("Rejection text added")]
    RejectionTextAdded = 421,

    /// <summary>Rejection checklist changed.</summary>
    [Description("Rejection checklist updated")]
    RejectionChecklistUpdated = 422,

    /// <summary>Resolution Center reply marked as sent.</summary>
    [Description("Rejection reply sent")]
    RejectionReplySent = 423,

    /// <summary>Rejection case closed.</summary>
    [Description("Rejection closed")]
    RejectionClosed = 424,

    /// <summary>Review reply sent (or edited).</summary>
    [Description("Review replied")]
    ReviewReplied = 430,

    /// <summary>Saved reply added, changed or removed.</summary>
    [Description("Saved reply changed")]
    SavedReplyChanged = 431,

    /// <summary>TestFlight group created or changed.</summary>
    [Description("Beta group changed")]
    BetaGroupChanged = 440,

    /// <summary>TestFlight testers added or removed.</summary>
    [Description("Beta testers changed")]
    BetaTestersChanged = 441,

    /// <summary>TestFlight texts changed (what to test, beta description).</summary>
    [Description("Beta texts changed")]
    BetaTextChanged = 442,

    /// <summary>Build submitted to beta app review.</summary>
    [Description("Submitted for beta review")]
    BetaReviewSubmitted = 443,
}
