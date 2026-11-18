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
    /// <summary>A storeasy app's catalog profile (device families, regions, features) changed.</summary>
    [Description("App profile updated")]
    AppProfileUpdated = 43,

    /// <summary>A change plan was approved and its apply job queued.</summary>
    [Description("Change plan approved")]
    PlanApproved = 50,

    /// <summary>A change plan was rejected.</summary>
    [Description("Change plan rejected")]
    PlanRejected = 51,

    /// <summary>A change plan was withdrawn.</summary>
    [Description("Change plan discarded")]
    PlanDiscarded = 52,

    /// <summary>A listing field was written to a store.</summary>
    [Description("Listing applied")]
    ListingApplied = 53,

    /// <summary>A screenshot, preview or graphic set was written to a store.</summary>
    [Description("Media applied")]
    MediaApplied = 54,

    /// <summary>A release (track release, release type) was written to a store.</summary>
    [Description("Release applied")]
    ReleaseApplied = 55,

    /// <summary>A staged or phased rollout changed.</summary>
    [Description("Rollout changed")]
    RolloutChanged = 56,

    /// <summary>A build was attached to a version or its export compliance answered.</summary>
    [Description("Build linked")]
    BuildLinked = 57,

    /// <summary>A version was submitted for review.</summary>
    [Description("Submitted for review")]
    SubmittedForReview = 58,

    /// <summary>Other store content (categories, age rating, pricing, review details, details, testers, data safety) was written.</summary>
    [Description("Store content applied")]
    ContentApplied = 59,

    /// <summary>Saved answers of an app changed.</summary>
    [Description("Saved answers updated")]
    AnswersUpdated = 60,

    /// <summary>Saved answers were copied from another app.</summary>
    [Description("Saved answers copied")]
    AnswersCopied = 61,

    /// <summary>An assisted step was handed off to a browser agent.</summary>
    [Description("Assisted step handed off")]
    AssistedStepHandedOff = 62,

    /// <summary>An assisted step was recorded as done or skipped.</summary>
    [Description("Assisted step completed")]
    AssistedStepCompleted = 63,

    /// <summary>A subscription group or subscription was written to a store.</summary>
    [Description("Subscription changed")]
    SubscriptionChanged = 80,

    /// <summary>A price or a price schedule was written to a store.</summary>
    [Description("Price scheduled")]
    PriceScheduled = 81,

    /// <summary>A subscription offer was written to a store.</summary>
    [Description("Offer changed")]
    OfferChanged = 82,

    /// <summary>An In-App Purchase or a Play one-time product was written to a store.</summary>
    [Description("In-App Purchase changed")]
    IapChanged = 83,

    /// <summary>A purchase setting (server notifications, sandbox tester) was written to a store.</summary>
    [Description("Purchase settings changed")]
    PurchaseSettingsChanged = 84,

    /// <summary>A mockup template was created.</summary>
    [Description("Mockup template created")]
    MockupTemplateCreated = 70,

    /// <summary>A mockup template's design, slots or locales changed.</summary>
    [Description("Mockup template updated")]
    MockupTemplateUpdated = 71,

    /// <summary>A mockup template was deleted.</summary>
    [Description("Mockup template deleted")]
    MockupTemplateDeleted = 72,

    /// <summary>Captions of a mockup template changed.</summary>
    [Description("Mockup captions updated")]
    MockupCaptionsUpdated = 73,

    /// <summary>A mockup export job was queued.</summary>
    [Description("Mockups exported")]
    MockupExported = 74,
}
