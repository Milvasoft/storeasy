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

    /// <summary>Organization logo uploaded or replaced.</summary>
    [Description("Organization logo set")]
    OrganizationLogoSet = 3,

    /// <summary>Organization logo removed.</summary>
    [Description("Organization logo removed")]
    OrganizationLogoRemoved = 4,

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

    /// <summary>Custom role created.</summary>
    [Description("Custom role created")]
    OrganizationRoleCreated = 15,

    /// <summary>Custom role renamed or its permissions changed.</summary>
    [Description("Custom role updated")]
    OrganizationRoleUpdated = 16,

    /// <summary>Custom role deleted.</summary>
    [Description("Custom role deleted")]
    OrganizationRoleDeleted = 17,

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

    /// <summary>The organization's Anthropic API key (AI reply drafts) was saved or replaced.</summary>
    [Description("AI key set")]
    AiKeySet = 24,

    /// <summary>The organization's Anthropic API key was removed.</summary>
    [Description("AI key removed")]
    AiKeyRemoved = 25,

    /// <summary>The models of the AI features (reply drafts, pre-review) were changed.</summary>
    [Description("AI models changed")]
    AiModelsChanged = 26,

    /// <summary>The key of an App Store Connect or Google Play connection was replaced in place (same connection).</summary>
    [Description("Store connection key replaced")]
    ConnectionKeyReplaced = 27,

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

    /// <summary>A storeasy app was hidden from the apps list.</summary>
    [Description("App hidden")]
    AppHidden = 44,

    /// <summary>A hidden storeasy app was shown in the apps list again.</summary>
    [Description("App shown")]
    AppUnhidden = 45,

    /// <summary>A member reordered the organization's apps list.</summary>
    [Description("Apps reordered")]
    AppsReordered = 46,

    /// <summary>A storeasy app's alerts were muted (the live board and the notifications leave it out).</summary>
    [Description("App alerts muted")]
    AppAlertsMuted = 47,

    /// <summary>A storeasy app's muted alerts were turned on again.</summary>
    [Description("App alerts unmuted")]
    AppAlertsUnmuted = 48,

    /// <summary>A storeasy app was deleted with its store records (Chrome, Firefox and winget only).</summary>
    [Description("App deleted")]
    AppDeleted = 49,

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

    /// <summary>A review reply draft was written with AI (saved as a draft, never sent).</summary>
    [Description("Reply draft written with AI")]
    ReviewReplyDraftGenerated = 432,

    /// <summary>An AI suggestion for a listing field was written (shown to the member, never saved or sent).</summary>
    [Description("Listing suggestion written with AI")]
    ListingSuggestionGenerated = 433,

    /// <summary>An AI suggestion for the App Store categories was written (shown to the member, never saved or sent).</summary>
    [Description("Category suggestion written with AI")]
    CategorySuggestionGenerated = 434,

    /// <summary>An AI pre-review of a store record was started (nothing is submitted).</summary>
    [Description("AI pre-review started")]
    PreReviewStarted = 450,

    /// <summary>An AI pre-review finished with a report.</summary>
    [Description("AI pre-review completed")]
    PreReviewCompleted = 451,

    /// <summary>Keywords were added to (or put back on) the daily rank tracking of a store record.</summary>
    [Description("ASO keywords tracked")]
    AsoKeywordsTracked = 470,

    /// <summary>Keywords were taken off the daily rank tracking; their rank history is kept.</summary>
    [Description("ASO keywords untracked")]
    AsoKeywordsUntracked = 471,

    /// <summary>The organization's AI proposed and grouped keyword seeds (the call's tokens and cost are in the details).</summary>
    [Description("ASO keyword ideas written with AI")]
    AsoKeywordSeedsGenerated = 472,

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

    /// <summary>A Microsoft Store change was queued (listings, images, packages, pricing, flights, add-ons, rollout).</summary>
    [Description("Microsoft Store change queued")]
    MsChangeQueued = 150,

    /// <summary>A Microsoft Store change was written to Partner Center.</summary>
    [Description("Microsoft Store change applied")]
    MsChangeApplied = 151,

    /// <summary>A Microsoft Store submission was committed (sent to certification) or an MSI/EXE product submitted.</summary>
    [Description("Microsoft Store submission sent")]
    MsSubmitted = 152,

    /// <summary>An MSI/EXE product was added by its product id.</summary>
    [Description("Microsoft Store product added")]
    MsProductAdded = 153,

    /// <summary>The client secret of a Microsoft Entra connection was replaced.</summary>
    [Description("Microsoft connection secret rotated")]
    MsConnectionSecretRotated = 154,

    /// <summary>A package flight group name was recorded.</summary>
    [Description("Microsoft flight group saved")]
    MsFlightGroupSaved = 155,

    /// <summary>A winget package was added by its package identifier.</summary>
    [Description("winget package added")]
    WingetPackageAdded = 160,

    /// <summary>A pull request with winget manifests was opened in winget-pkgs from the fork.</summary>
    [Description("winget pull request opened")]
    WingetPullRequestOpened = 161,

    /// <summary>A winget pull request changed review state (validation, feedback, approval).</summary>
    [Description("winget review state changed")]
    WingetReviewStateChanged = 162,

    /// <summary>A winget pull request was merged: the version is published.</summary>
    [Description("winget version published")]
    WingetPublished = 163,

    /// <summary>A winget pull request was closed without a merge.</summary>
    [Description("winget pull request closed")]
    WingetPullRequestClosed = 164,

    /// <summary>A Chrome Web Store item was added by its extension id.</summary>
    [Description("Chrome item added")]
    ChromeItemAdded = 170,

    /// <summary>An extension package was uploaded to a Chrome Web Store item.</summary>
    [Description("Chrome package uploaded")]
    ChromePackageUploaded = 171,

    /// <summary>A Chrome Web Store item was submitted for review (or published at once when review was skipped).</summary>
    [Description("Chrome item submitted")]
    ChromeSubmitted = 172,

    /// <summary>The submitted revision of a Chrome Web Store item changed state (pending review, staged, cancelled).</summary>
    [Description("Chrome review state changed")]
    ChromeReviewStateChanged = 173,

    /// <summary>A new revision of a Chrome Web Store item was published.</summary>
    [Description("Chrome item published")]
    ChromePublished = 174,

    /// <summary>A Chrome Web Store submission was rejected.</summary>
    [Description("Chrome submission rejected")]
    ChromeRejected = 175,

    /// <summary>The deploy percentage of a published Chrome Web Store revision was raised.</summary>
    [Description("Chrome deploy percentage raised")]
    ChromeDeployPercentageChanged = 176,

    /// <summary>The pending submission of a Chrome Web Store item was cancelled.</summary>
    [Description("Chrome submission cancelled")]
    ChromeSubmissionCancelled = 177,

    /// <summary>A Chrome Web Store item was rolled back to an earlier package.</summary>
    [Description("Chrome item rolled back")]
    ChromeRolledBack = 178,

    /// <summary>A Chrome Web Store item was warned or taken down for a policy violation, or the flag was cleared.</summary>
    [Description("Chrome policy flag changed")]
    ChromePolicyFlagChanged = 179,

    /// <summary>A Firefox add-on was added to storeasy (from the owner's add-ons or by its id).</summary>
    [Description("Firefox add-on added")]
    FirefoxAddonAdded = 180,

    /// <summary>A new version of a Firefox add-on was uploaded and submitted (listed or unlisted channel).</summary>
    [Description("Firefox version submitted")]
    FirefoxVersionSubmitted = 181,

    /// <summary>The listing of a Firefox add-on (texts, links, categories) was changed.</summary>
    [Description("Firefox listing updated")]
    FirefoxListingUpdated = 182,

    /// <summary>Previews (screenshots) of a Firefox add-on were added or removed.</summary>
    [Description("Firefox previews changed")]
    FirefoxPreviewsChanged = 183,

    /// <summary>The icon of a Firefox add-on was replaced.</summary>
    [Description("Firefox icon updated")]
    FirefoxIconUpdated = 184,

    /// <summary>The review state of a Firefox version changed (awaiting review, approved, disabled).</summary>
    [Description("Firefox review state changed")]
    FirefoxReviewStateChanged = 185,

    /// <summary>A Firefox version was approved and signed (its file is public).</summary>
    [Description("Firefox version approved")]
    FirefoxApproved = 186,

    /// <summary>A Firefox version was rejected or disabled.</summary>
    [Description("Firefox version disabled")]
    FirefoxRejected = 187,

    /// <summary>The status of a Firefox add-on changed (approved, awaiting review, disabled by Mozilla or the developer).</summary>
    [Description("Firefox add-on status changed")]
    FirefoxAddonStatusChanged = 188,

    /// <summary>The release notes of a Firefox version were changed.</summary>
    [Description("Firefox release notes updated")]
    FirefoxReleaseNotesUpdated = 189,

    /// <summary>A personal access token was created.</summary>
    [Description("Token created")]
    TokenCreated = 300,

    /// <summary>A personal access token was revoked.</summary>
    [Description("Token revoked")]
    TokenRevoked = 301,

    /// <summary>A CI token was created.</summary>
    [Description("CI token created")]
    CiTokenCreated = 302,

    /// <summary>A CI token was revoked.</summary>
    [Description("CI token revoked")]
    CiTokenRevoked = 303,

    /// <summary>A trusted publisher rule was added.</summary>
    [Description("Trusted publisher added")]
    TrustedPublisherAdded = 304,

    /// <summary>A trusted publisher rule was removed.</summary>
    [Description("Trusted publisher removed")]
    TrustedPublisherRemoved = 305,

    /// <summary>The delivery rules of an app changed.</summary>
    [Description("Delivery rules updated")]
    DeliveryRulesUpdated = 306,

    /// <summary>A pull source (EAS or generic webhook) was saved or its secret rotated.</summary>
    [Description("Artifact source updated")]
    ArtifactSourceUpdated = 307,

    /// <summary>A package upload was completed and queued for processing.</summary>
    [Description("Package received")]
    ArtifactReceived = 308,

    /// <summary>A package was delivered to a store.</summary>
    [Description("Package delivered")]
    ArtifactDelivered = 309,

    /// <summary>A package was rejected (wrong app, failed check).</summary>
    [Description("Package rejected")]
    ArtifactRejected = 310,

    /// <summary>A package was held for a member before delivery.</summary>
    [Description("Package held")]
    ArtifactHeld = 311,

    /// <summary>A stored package was deleted.</summary>
    [Description("Package deleted")]
    ArtifactDeleted = 312,

    /// <summary>The package intake settings (retention) changed.</summary>
    [Description("Intake settings updated")]
    IntakeSettingsUpdated = 313,

    /// <summary>A member asked for a download link of a stored build package.</summary>
    [Description("Build package downloaded")]
    BuildPackageDownloaded = 314,

    /// <summary>A member asked App Store Connect to start the analytics reports of an app.</summary>
    [Description("App Store analytics enabled")]
    AppleAnalyticsEnabled = 480,

    /// <summary>The Cloud Storage bucket of a Google Play connection's reports was set or cleared.</summary>
    [Description("Play reports bucket updated")]
    ReportBucketUpdated = 481,

    /// <summary>
    /// A store listing experiment record was created.
    /// </summary>
    [Description("Experiment created")]
    AsoExperimentCreated = 490,

    /// <summary>
    /// A store listing experiment record was edited (variants, checklist).
    /// </summary>
    [Description("Experiment updated")]
    AsoExperimentUpdated = 491,

    /// <summary>
    /// A store listing experiment record was deleted.
    /// </summary>
    [Description("Experiment deleted")]
    AsoExperimentDeleted = 492,

    /// <summary>
    /// An approved plan wrote an experiment or custom product page to the store.
    /// </summary>
    [Description("Experiment written to the store")]
    AsoExperimentWritten = 493,

    /// <summary>
    /// An approved plan started an experiment.
    /// </summary>
    [Description("Experiment started")]
    AsoExperimentStarted = 494,

    /// <summary>
    /// An approved plan stopped an experiment.
    /// </summary>
    [Description("Experiment stopped")]
    AsoExperimentStopped = 495,

    /// <summary>
    /// An approved plan wrote the winning treatment to the app version.
    /// </summary>
    [Description("Experiment winner applied")]
    AsoExperimentWinnerApplied = 496,

    /// <summary>
    /// A member recorded the start, end or result of an experiment.
    /// </summary>
    [Description("Experiment result recorded")]
    AsoExperimentResultRecorded = 497,

    /// <summary>
    /// An approved plan submitted an experiment or custom product page for App Review.
    /// </summary>
    [Description("Experiment submitted for review")]
    AsoExperimentSubmitted = 498,

    /// <summary>A package was added to the package registries module.</summary>
    [Description("Package tracked")]
    RegistryPackageAdded = 500,

    /// <summary>A package was removed from the package registries module.</summary>
    [Description("Package untracked")]
    RegistryPackageRemoved = 501,

    /// <summary>A new version of a tracked package appeared on its registry.</summary>
    [Description("Package version published")]
    RegistryVersionPublished = 502,

    /// <summary>A version of a tracked package was deprecated.</summary>
    [Description("Package version deprecated")]
    RegistryVersionDeprecated = 503,

    /// <summary>A version of a tracked package was yanked or unlisted.</summary>
    [Description("Package version yanked")]
    RegistryVersionYanked = 504,

    /// <summary>A security advisory was found for the latest version of a tracked package.</summary>
    [Description("Package security advisory")]
    RegistryAdvisoryFound = 505,

    /// <summary>The trusted publishing setup of a tracked package was saved or cleared.</summary>
    [Description("Trusted publishing setup saved")]
    RegistrySetupSaved = 506,

    /// <summary>The latest version of a tracked package did not come from the expected repository or workflow.</summary>
    [Description("Package publisher mismatch")]
    RegistryPublisherMismatch = 507,

    /// <summary>A registry token reminder was added or changed (the token itself is never stored).</summary>
    [Description("Token reminder saved")]
    RegistryTokenReminderSaved = 508,

    /// <summary>A registry token reminder was removed.</summary>
    [Description("Token reminder removed")]
    RegistryTokenReminderRemoved = 509,

    /// <summary>A recorded registry token expires soon or has expired.</summary>
    [Description("Registry token expiring")]
    RegistryTokenExpiring = 510,

    /// <summary>A package group was added or changed.</summary>
    [Description("Package group saved")]
    RegistryGroupSaved = 511,

    /// <summary>A package group was removed.</summary>
    [Description("Package group removed")]
    RegistryGroupRemoved = 512,
}
