using Storeasy.Application.Features.Answers.CopySavedAnswers;
using Storeasy.Application.Features.Answers.GetSavedAnswers;
using Storeasy.Application.Features.Answers.PlanDataSafety;
using Storeasy.Application.Features.Answers.UpdateSavedAnswers;
using Storeasy.Application.Features.Apps.AddGoogleApp;
using Storeasy.Application.Features.Apps.GetAppDetail;
using Storeasy.Application.Features.Apps.GetAppList;
using Storeasy.Application.Features.Apps.LinkStoreApp;
using Storeasy.Application.Features.Apps.ReorderApps;
using Storeasy.Application.Features.Apps.SetAppAlertsMuted;
using Storeasy.Application.Features.Apps.SetAppHidden;
using Storeasy.Application.Features.Apps.SyncApps;
using Storeasy.Application.Features.Apps.UnlinkStoreApp;
using Storeasy.Application.Features.Apps.UpdateAppProfile;
using Storeasy.Application.Features.Artifacts.DeliverArtifactSession;
using Storeasy.Application.Features.Artifacts.GetArtifactSessionDetail;
using Storeasy.Application.Features.Artifacts.GetArtifactSessionList;
using Storeasy.Application.Features.Artifacts.GetArtifactSummary;
using Storeasy.Application.Features.AsoExperiments.GetAsoExperimentDetail;
using Storeasy.Application.Features.AsoExperiments.GetAsoExperimentExport;
using Storeasy.Application.Features.AsoExperiments.GetAsoExperimentOverview;
using Storeasy.Application.Features.AsoExperiments.PlanAsoExperiment;
using Storeasy.Application.Features.AsoExperiments.RecordAsoExperimentResult;
using Storeasy.Application.Features.AsoExperiments.SaveAsoExperiment;
using Storeasy.Application.Features.AsoExperiments.SyncAsoExperiments;
using Storeasy.Application.Features.AsoExperiments.UpdateAsoExperimentChecklist;
using Storeasy.Application.Features.AssistedSteps.CompleteAssistedStep;
using Storeasy.Application.Features.AssistedSteps.GetAssistedStepDetail;
using Storeasy.Application.Features.AssistedSteps.GetAssistedStepList;
using Storeasy.Application.Features.AssistedSteps.HandOffAssistedStep;
using Storeasy.Application.Features.Builds.GetBuildDownloadUrl;
using Storeasy.Application.Features.Builds.GetBuildList;
using Storeasy.Application.Features.Builds.PlanBuildLink;
using Storeasy.Application.Features.Builds.PlanBundleUpload;
using Storeasy.Application.Features.Builds.PlanExportCompliance;
using Storeasy.Application.Features.ChangePlans.DiscardChangePlan;
using Storeasy.Application.Features.ChangePlans.GetChangePlanDetail;
using Storeasy.Application.Features.ChangePlans.GetChangePlanList;
using Storeasy.Application.Features.ChangePlans.RejectChangePlan;
using Storeasy.Application.Features.DeliveryRules.GetDeliveryRules;
using Storeasy.Application.Features.Iap.GetIapDetail;
using Storeasy.Application.Features.Iap.GetIapList;
using Storeasy.Application.Features.Iap.GetIapPricePoints;
using Storeasy.Application.Features.Iap.GetIapPrices;
using Storeasy.Application.Features.Iap.PlanIapChanges;
using Storeasy.Application.Features.Listings.GetAgeRating;
using Storeasy.Application.Features.AsoImpact.GetAsoImpact;
using Storeasy.Application.Features.AsoImpact.GetAsoImpactEvents;
using Storeasy.Application.Features.AsoImpact.GetAsoImpactRecent;
using Storeasy.Application.Features.AsoKeywords.GetAsoKeywordCoverage;
using Storeasy.Application.Features.AsoKeywords.GetAsoKeywordRanks;
using Storeasy.Application.Features.AsoKeywords.GetAsoKeywordSeeds;
using Storeasy.Application.Features.AsoKeywords.GetAsoTrackedKeywords;
using Storeasy.Application.Features.AsoKeywords.ResearchAsoKeywords;
using Storeasy.Application.Features.AsoKeywords.SuggestAsoKeywordSeeds;
using Storeasy.Application.Features.AsoKeywords.TrackAsoKeywords;
using Storeasy.Application.Features.AsoKeywords.UntrackAsoKeywords;
using Storeasy.Application.Features.Listings.GetCategories;
using Storeasy.Application.Features.Listings.GetListing;
using Storeasy.Application.Features.Listings.GetListingByField;
using Storeasy.Application.Features.Listings.GetListingDetails;
using Storeasy.Application.Features.Listings.GetPricePoints;
using Storeasy.Application.Features.Listings.GetPricing;
using Storeasy.Application.Features.Listings.GetReviewInfo;
using Storeasy.Application.Features.Listings.PlanAgeRating;
using Storeasy.Application.Features.Listings.PlanCategories;
using Storeasy.Application.Features.Listings.PlanListing;
using Storeasy.Application.Features.Listings.PlanPricing;
using Storeasy.Application.Features.Listings.PlanReviewInfo;
using Storeasy.Application.Features.Listings.SuggestCategories;
using Storeasy.Application.Features.Listings.SuggestListingField;
using Storeasy.Application.Features.Media.GetMediaSets;
using Storeasy.Application.Features.Media.PlanMediaSet;
using Storeasy.Application.Features.Mockups.CreateMockupTemplate;
using Storeasy.Application.Features.Mockups.ExportMockups;
using Storeasy.Application.Features.Mockups.GetMockupExportManifest;
using Storeasy.Application.Features.Mockups.GetMockupExportPlan;
using Storeasy.Application.Features.Mockups.GetMockupFrames;
using Storeasy.Application.Features.Mockups.GetMockupStarterTemplates;
using Storeasy.Application.Features.Mockups.GetMockupTemplateDetail;
using Storeasy.Application.Features.Mockups.GetMockupTemplateList;
using Storeasy.Application.Features.Mockups.RenderMockupPreview;
using Storeasy.Application.Features.Mockups.SaveMockupCaptions;
using Storeasy.Application.Features.Mockups.SuggestMockupCaptions;
using Storeasy.Application.Features.Mockups.UpdateMockupTemplate;
using Storeasy.Application.Features.MsStore.AddMsProduct;
using Storeasy.Application.Features.MsStore.ApplyMsChange;
using Storeasy.Application.Features.MsStore.CheckMsInstaller;
using Storeasy.Application.Features.MsStore.GetMsAddonList;
using Storeasy.Application.Features.MsStore.GetMsContent;
using Storeasy.Application.Features.MsStore.GetMsFlightList;
using Storeasy.Application.Features.MsStore.GetMsInstallerPackageList;
using Storeasy.Application.Features.MsStore.GetMsProductDetail;
using Storeasy.Application.Features.MsStore.GetMsSubmissionList;
using Storeasy.Application.Features.MsStore.SaveMsFlightGroup;
using Storeasy.Application.Features.Organizations.GetMemberList;
using Storeasy.Application.Features.PurchaseSettings.GetSandboxTesterList;
using Storeasy.Application.Features.PurchaseSettings.GetServerNotifications;
using Storeasy.Application.Features.PurchaseSettings.PlanSandboxTesters;
using Storeasy.Application.Features.PurchaseSettings.PlanServerNotifications;
using Storeasy.Application.Features.Readiness.GetAppCompliance;
using Storeasy.Application.Features.Readiness.GetAppsOverview;
using Storeasy.Application.Features.Readiness.GetOrganizationCompliance;
using Storeasy.Application.Features.Readiness.GetReadiness;
using Storeasy.Application.Features.PreReviews.GetPreReviewDetail;
using Storeasy.Application.Features.PreReviews.GetPreReviewList;
using Storeasy.Application.Features.Rejections.CloseRejectionCase;
using Storeasy.Application.Features.Rejections.GetRejectionCaseDetail;
using Storeasy.Application.Features.Rejections.GetRejectionCaseList;
using Storeasy.Application.Features.Rejections.GetRejectionHistory;
using Storeasy.Application.Features.Rejections.MarkRejectionReplySent;
using Storeasy.Application.Features.Rejections.OpenRejectionCase;
using Storeasy.Application.Features.Rejections.UpdateRejectionChecklist;
using Storeasy.Application.Features.Rejections.UpdateRejectionReplyDraft;
using Storeasy.Application.Features.Rejections.UpdateRejectionText;
using Storeasy.Application.Features.Releases.GetPlayTesters;
using Storeasy.Application.Features.Releases.GetReleaseOverview;
using Storeasy.Application.Features.Releases.PlanPlayTesters;
using Storeasy.Application.Features.Releases.PlanRelease;
using Storeasy.Application.Features.Releases.PlanRollout;
using Storeasy.Application.Features.Reviews.CreateSavedReply;
using Storeasy.Application.Features.StoreMetrics.EnableAppleAnalytics;
using Storeasy.Application.Features.StoreMetrics.GetStoreMetricFeeds;
using Storeasy.Application.Features.StoreMetrics.GetStoreMetrics;
using Storeasy.Application.Features.StoreMetrics.GetStoreMetricsSummary;
using Storeasy.Application.Features.StoreMetrics.SyncStoreMetrics;
using Storeasy.Application.Features.Reviews.DeleteSavedReply;
using Storeasy.Application.Features.Reviews.GetReviewList;
using Storeasy.Application.Features.Reviews.GetSavedReplyList;
using Storeasy.Application.Features.Reviews.MarkReviewsRead;
using Storeasy.Application.Features.Reviews.ReplyToReview;
using Storeasy.Application.Features.Reviews.SaveReviewReplyDraft;
using Storeasy.Application.Features.Reviews.SyncReviews;
using Storeasy.Application.Features.Reviews.UpdateSavedReply;
using Storeasy.Application.Features.Rules.GetRuleCatalog;
using Storeasy.Application.Features.Rules.GetRuleDetail;
using Storeasy.Application.Features.Rules.GetRuleList;
using Storeasy.Application.Features.StoreAuditLogs.GetStoreAuditLogList;
using Storeasy.Application.Features.StoreConnections.GetStoreConnectionList;
using Storeasy.Application.Features.StoreJobs.CancelStoreJob;
using Storeasy.Application.Features.StoreJobs.GetStoreJobDetail;
using Storeasy.Application.Features.StoreJobs.GetStoreJobList;
using Storeasy.Application.Features.Subscriptions.ConvertSubscriptionPrices;
using Storeasy.Application.Features.Subscriptions.GetSubscriptionDetail;
using Storeasy.Application.Features.Subscriptions.GetSubscriptionGroups;
using Storeasy.Application.Features.Subscriptions.GetSubscriptionOffers;
using Storeasy.Application.Features.Subscriptions.GetSubscriptionPricePoints;
using Storeasy.Application.Features.Subscriptions.GetSubscriptionPrices;
using Storeasy.Application.Features.Subscriptions.PlanSubscriptionChanges;
using Storeasy.Application.Features.Subscriptions.SyncStoreProducts;
using Storeasy.Application.Features.TestFlight.CreateBetaGroup;
using Storeasy.Application.Features.TestFlight.GetBetaBuildList;
using Storeasy.Application.Features.TestFlight.GetBetaGroupList;
using Storeasy.Application.Features.TestFlight.GetBetaLocalizationList;
using Storeasy.Application.Features.TestFlight.GetBetaTesterList;
using Storeasy.Application.Features.TestFlight.SubmitBetaReview;
using Storeasy.Application.Features.TestFlight.SyncTestFlight;
using Storeasy.Application.Features.TestFlight.UpdateBetaGroup;
using Storeasy.Application.Features.TestFlight.UpdateBetaGroupBuilds;
using Storeasy.Application.Features.TestFlight.UpdateBetaGroupTesters;
using Storeasy.Application.Features.TestFlight.UpdateBetaLocalization;
using Storeasy.Application.Features.TestFlight.UpdateWhatToTest;
using Storeasy.Domain.Enums;


namespace Storeasy.Api.Mcp;

/// <summary>
/// The tools of the remote MCP (docs/urun-plani.md 2.6, 11.7): every API row of the coverage matrix that exists, named
/// after the store-ops tools where they overlap (one storeasy tool covers both stores). Store writes are plans only: a plan
/// tool returns the diff and the plan id, <c>plans_approve</c> applies it with the same permission and typed confirmation as
/// the web (<c>confirm: true</c> plus <c>confirmText</c> for review submission and production). Connections are read only
/// here; tokens and secrets are never handled by MCP. <c>artifact_upload</c> belongs to the local bridge only.
/// </summary>
public static class McpToolCatalog
{
    private const string _planNote = " Returns a change plan (diff, warnings, store calls); nothing is written until plans_approve with the plan id.";

    private static readonly string[] _imageTypes = [".png", ".jpg", ".jpeg"];

    /// <summary>
    /// Every tool, in listing order.
    /// </summary>
    public static IReadOnlyList<McpToolDefinition> Tools { get; } = Build();

    /// <summary>
    /// Tool by name, or null.
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static McpToolDefinition Find(string name) => Tools.FirstOrDefault(t => t.Name == name);

    private static List<McpToolDefinition> Build() => [.. PlainTools(), .. McpCustomTools.All];

    private static List<McpToolDefinition> PlainTools() =>
    [
        // Apps and overview
        Read<GetAppsOverviewQuery>("apps_summary", "One call summary of every app in both stores: state, version, readiness and what is missing."),
        Read<GetAppListQuery>("apps_list", "Lists the organization's apps with their store records (App Store Connect app id, Play package)."),
        Read<GetAppDetailQuery>("apps_get", "Reads one app with its store records, profile and builds summary."),
        Write<SyncAppsCommand>("apps_sync", "Queues a sync of apps and builds from the store connections (a store job)."),
        Write<AddGoogleAppCommand>("apps_add_google", "Adds a Google Play app by package name and checks access with an edit probe."),
        Write<LinkStoreAppCommand>("apps_link_store_record", "Moves a store record to another storeasy app (links the App Store and Play records of one product)."),
        Write<UnlinkStoreAppCommand>("apps_unlink_store_record", "Moves a store record out to its own app."),
        Write<UpdateAppProfileCommand>("apps_profile_update", "Updates the catalog profile of an app (device families, regions, features, SDK, target API) used by the readiness check."),
        Write<SetAppHiddenCommand>("apps_hide", "Hides an app from the apps list and the organization wide overviews; nothing is deleted and sync keeps it hidden.", ("Hidden", true)),
        Write<SetAppHiddenCommand>("apps_unhide", "Shows a hidden app in the apps list again (apps_list with Hidden true lists the hidden ones).", ("Hidden", false)),
        Write<SetAppAlertsMutedCommand>("apps_mute_alerts", "Mutes an app's alerts: the live activity board and the notifications to members leave it out; the app stays in the lists, keeps syncing and its records are kept.", ("Muted", true)),
        Write<SetAppAlertsMutedCommand>("apps_unmute_alerts", "Turns a muted app's alerts on again (apps_list shows AlertsMutedDate on muted apps).", ("Muted", false)),
        Write<ReorderAppsCommand>("apps_reorder", "Sets the order of the apps list: the given app ids come first in that order, every other app follows by name (apps_list without a sort shows this order)."),

        // Readiness and compliance
        Read<GetReadinessQuery>("readiness_check", "What blocks a submission of one store record, from one live read of the store and the rules catalog."),
        Read<GetAppComplianceQuery>("compliance_app", "Rules catalog deadlines and their state for one app."),
        Read<GetOrganizationComplianceQuery>("compliance_list", "Rules catalog deadlines across every app of the organization."),
        Read<GetPreReviewListQuery>("pre_reviews_list", "AI pre-review history of one app in one store, newest first: verdict, approval likelihood (an AI estimate), finding counts. Start one in the web app."),
        Read<GetPreReviewDetailQuery>("pre_reviews_get", "Reads one AI pre-review: verdict, approval likelihood, findings with the store guideline and fix, what a reviewer will test at runtime."),

        // Listings and media
        Read<GetListingQuery>("listings_get", "Reads the store listing texts of a store record, per locale."),
        Read<GetListingByFieldQuery>("listings_get_by_field", "Reads one listing field across every locale."),
        Read<GetListingDetailsQuery>("listings_details_get", "Reads the app level details (contact, privacy URL, Play details)."),
        Write<SuggestListingFieldCommand>("listings_suggest", "Suggests one listing text (App Store name, subtitle, description, keywords, promotionalText, whatsNew; Play title, shortDescription, fullDescription) with the organization's AI key, within the catalog limits. Nothing is saved: review it and plan it with listings_plan. Audited with tokens and cost."),
        Plan<PlanListingCommand>("listings_plan", "Plans listing text changes (per locale and field), checked against the catalog limits."),
        Read<GetMediaSetsQuery>("screenshots_sets", "Lists the screenshot and preview sets of a store record with the catalog's required classes.", ("Kind", MediaKind.Screenshot)),
        Plan<PlanMediaSetCommand>("screenshots_plan", "Plans one screenshot set (locale and class): uploads, deletes and order. Files are checked against the catalog.",
                                  [("Kind", MediaKind.Screenshot)], [], [("AssetIds", new McpUploadSpec("files", 1, _imageTypes))]),
        Read<GetMediaSetsQuery>("previews_sets", "Lists the app preview (video) sets of a store record.", ("Kind", MediaKind.Preview)),
        Plan<PlanMediaSetCommand>("previews_plan", "Plans one app preview set (locale and class).",
                                  [("Kind", MediaKind.Preview)], [], [("AssetIds", new McpUploadSpec("files", 1, ".mp4", ".mov", ".m4v"))]),
        Read<GetCategoriesQuery>("categories_get", "Reads the categories of a store record and the store's choices."),
        Write<SuggestCategoriesCommand>("categories_suggest", "Suggests the App Store primary and secondary category from the store's list with the organization's AI key. Nothing is saved: review it and plan it with categories_plan. Audited with tokens and cost."),
        Plan<PlanCategoriesCommand>("categories_plan", "Plans category changes."),
        Read<GetAgeRatingQuery>("age_rating_get", "Reads the age rating answers (including the newest questions)."),
        Plan<PlanAgeRatingCommand>("age_rating_plan", "Plans age rating answers."),
        Read<GetPricingQuery>("pricing_get", "Reads the price schedule and availability."),
        Read<GetPricePointsQuery>("pricing_price_points", "Lists the App Store price points of a territory."),
        Plan<PlanPricingCommand>("pricing_plan", "Plans price and availability changes."),
        Read<GetReviewInfoQuery>("review_info_get", "Reads the App Review contact, demo account (never the password) and notes."),
        Plan<PlanReviewInfoCommand>("review_info_plan", "Plans App Review details; the demo password comes from the saved answers."),

        // Builds and releases
        Read<GetBuildListQuery>("builds_list", "Lists the builds of a store record (processing state, version, Play tracks, TestFlight expiry)."),
        Read<GetBuildDownloadUrlQuery>("builds_download_url", "Returns a short lived (30 minute) download URL of the package (IPA, AAB, APK, MSIX) storeasy keeps for a build; builds_list shows which builds have one (PackageSessionId). Audited."),
        Plan<PlanBuildLinkCommand>("builds_link", "Plans attaching a VALID build to the App Store version."),
        Plan<PlanExportComplianceCommand>("builds_export_compliance", "Plans the export compliance answer of a build."),
        Plan<PlanBundleUploadCommand>("bundle_upload_plan", "Plans uploading an Android App Bundle (an uploaded asset) to Google Play, optionally onto a track. Production needs ReleaseProduction and the typed confirmation on approval.",
                                      [], [], [("AssetId", new McpUploadSpec("file", 4, ".aab"))]),
        Read<GetReleaseOverviewQuery>("releases_get", "Reads versions, release type, phased release and Play tracks."),
        Plan<PlanReleaseCommand>("releases_plan", "Plans a release change (release type, phased release, Play track release, countries, notes)."),
        Plan<PlanRolloutCommand>("rollout_plan", "Plans a staged or phased rollout change (fraction, pause, resume, complete)."),
        Read<GetPlayTestersQuery>("testers_get", "Reads the Google Groups of the Play testing tracks."),
        Plan<PlanPlayTestersCommand>("testers_plan", "Plans the Google Groups of a Play testing track."),

        // Saved answers and assisted steps
        Read<GetSavedAnswersQuery>("answers_get", "Reads an app's saved answers (privacy labels, data safety, content rating, review login without the password)."),
        Write<UpdateSavedAnswersCommand>("answers_update", "Updates saved answers of an app (storeasy data, not a store write)."),
        Write<CopySavedAnswersCommand>("answers_copy", "Copies saved answers from another app."),
        Plan<PlanDataSafetyCommand>("data_safety_plan", "Plans sending the Data safety answers to Google Play."),
        Read<GetAssistedStepListQuery>("assisted_steps_list", "Lists the assisted steps (store work without an API) and their state."),
        Read<GetAssistedStepDetailQuery>("assisted_step_get", "Reads one assisted step with its checklist and saved answers."),
        Write<HandOffAssistedStepCommand>("assisted_step_handoff", "Prepares the browser hand off of an assisted step (prompt and links)."),
        Write<CompleteAssistedStepCommand>("assisted_step_complete", "Records an assisted step as done or skipped."),

        // Plans and jobs
        Read<GetChangePlanListQuery>("plans_list", "Lists change plans (pending, approved, applied, rejected, expired)."),
        Read<GetChangePlanDetailQuery>("plans_get", "Reads one change plan with its diff, warnings, store calls and job error."),
        Write<RejectChangePlanCommand>("plans_reject", "Rejects a pending plan with a reason (needs the plan's own permission)."),
        Write<DiscardChangePlanCommand>("plans_discard", "Discards a pending plan without writing anything."),
        Read<GetStoreJobListQuery>("jobs_list", "Lists store jobs, newest first."),
        Read<GetStoreJobDetailQuery>("jobs_get", "Reads one store job with its steps."),
        Write<CancelStoreJobCommand>("jobs_cancel", "Cancels a queued job, or asks a running one to stop at its next step."),

        // Rules, connections, audit, members
        Read<GetRuleCatalogQuery>("rules_catalog", "Version and summary of the store rules catalog."),
        Read<GetRuleListQuery>("rules_list", "Lists catalog rules with filters (store, category, status)."),
        Read<GetRuleDetailQuery>("rules_get", "Reads one catalog rule with its sources."),
        Read<GetStoreConnectionListQuery>("connections_list", "Lists the store connections (read only; secrets are never returned)."),
        Read<GetStoreAuditLogListQuery>("audit_list", "Pages through the organization's audit log."),
        Read<GetMemberListQuery>("members_list", "Lists the organization's members and roles."),

        // Package intake
        Read<GetArtifactSummaryQuery>("artifacts_summary", "The last package of every app and its delivery rules."),
        Read<GetArtifactSessionListQuery>("artifacts_sessions", "Lists package upload sessions (CI, CLI, webhooks, manual)."),
        Read<GetArtifactSessionDetailQuery>("artifacts_session_get", "Reads one upload session: steps, package, checks, delivery target and CI result."),
        Write<DeliverArtifactSessionCommand>("artifacts_deliver_held", "Delivers a held package to its test channel (never production or review)."),
        Read<GetDeliveryRulesQuery>("delivery_rules_get", "Reads the delivery rules of an app."),

        // Reviews
        Read<GetReviewListQuery>("reviews_list", "The review inbox of every app (App Store and Google Play), newest first."),
        Write<SyncReviewsCommand>("reviews_sync", "Reads new reviews now (a store job per store record)."),
        Write<MarkReviewsReadCommand>("reviews_mark_read", "Marks reviews read or unread for the caller."),
        Write<SaveReviewReplyDraftCommand>("reviews_reply_draft", "Saves a reply draft for a review; nothing goes to the store. A member publishes it (or reviews_reply)."),
        Write<ReplyToReviewCommand>("reviews_reply", "Publishes (or replaces) the reply of a review as a store job (ReplyReviews permission). Google Play 350, App Store 5,970 characters."),
        Read<GetSavedReplyListQuery>("reviews_saved_replies", "Lists the saved replies (optionally for a locale or app)."),
        Write<CreateSavedReplyCommand>("reviews_saved_reply_create", "Adds a saved reply."),
        Write<UpdateSavedReplyCommand>("reviews_saved_reply_update", "Changes a saved reply."),
        Write<DeleteSavedReplyCommand>("reviews_saved_reply_delete", "Removes a saved reply."),

        // Store page performance (ASO conversion analytics)
        Read<GetStoreMetricsQuery>("aso_metrics_get", "Store page performance of one app from storeasy's daily rows: impressions (App Store), page views, downloads, redownloads (App Store) and conversion rate (downloads / page views), totals with the change against the previous period, daily series and breakdowns by traffic source, country and store. Filters: store, country, source; range: from/to or days (default the last 30 days ending yesterday)."),
        Read<GetStoreMetricsSummaryQuery>("aso_metrics_summary", "Store page performance of every app of the organization over a date range, with the change against the previous period."),
        Read<GetStoreMetricFeedsQuery>("aso_metrics_feeds", "Per store record of an app: whether metrics arrive, the last read, how far the data goes and what to set up (App Store analytics reports, Play reports bucket, a missing role)."),
        Write<SyncStoreMetricsCommand>("aso_metrics_sync", "Reads the store page metrics now (a read only store job per store record; they are also read daily)."),
        Write<EnableAppleAnalyticsCommand>("aso_analytics_enable", "Asks App Store Connect to start the ONGOING analytics reports of an App Store record (ManageConnections permission; the API key needs the Admin role). A one time store write: needs confirm: true. The first data arrives one or two days later."),

        // Rejections
        Read<GetRejectionCaseListQuery>("rejections_list", "Pages through the rejection cases, newest first."),
        Read<GetRejectionCaseDetailQuery>("rejections_get", "Reads one rejection case: text, checklist, reply draft and timeline."),
        Read<GetRejectionHistoryQuery>("rejections_history", "Submission history of an app with its rejection cases."),
        Write<OpenRejectionCaseCommand>("rejections_open_case", "Opens a rejection case by hand (when no webhook or sync reported it)."),
        Write<UpdateRejectionTextCommand>("rejections_paste_text", "Saves the Resolution Center text of a case; the cited guidelines become checklist items."),
        Write<UpdateRejectionChecklistCommand>("rejections_checklist_update", "Replaces the checklist of a case (items, statuses, assignees)."),
        Write<UpdateRejectionReplyDraftCommand>("rejections_reply_draft", "Saves the Resolution Center reply draft (storeasy never sends it; the API cannot)."),
        Write<MarkRejectionReplySentCommand>("rejections_mark_reply_sent", "Records that the reply was sent in the Resolution Center."),
        Write<CloseRejectionCaseCommand>("rejections_close", "Closes a case by hand."),

        // TestFlight
        Read<GetBetaGroupListQuery>("testflight_groups", "Lists the TestFlight beta groups of an App Store record."),
        Read<GetBetaTesterListQuery>("testflight_testers", "Pages through the TestFlight testers (optionally of one group)."),
        Read<GetBetaBuildListQuery>("testflight_builds", "Lists the TestFlight builds with expiry, beta review state and groups."),
        Read<GetBetaLocalizationListQuery>("testflight_localizations", "Lists the beta app descriptions and feedback e-mails per language."),
        Write<SyncTestFlightCommand>("testflight_sync", "Reads TestFlight groups, testers, builds and texts now (a store job)."),
        Write<CreateBetaGroupCommand>("testflight_group_create", "Creates a beta group (a store job)."),
        Write<UpdateBetaGroupCommand>("testflight_group_update", "Renames a beta group or changes its public link."),
        Write<UpdateBetaGroupTestersCommand>("testflight_testers_update", "Invites testers to a beta group by e-mail or removes them."),
        Write<UpdateBetaGroupBuildsCommand>("testflight_add_build", "Adds builds to (or removes them from) a beta group."),
        Write<UpdateWhatToTestCommand>("testflight_what_to_test", "Writes the What to Test text of a build in one language."),
        Write<UpdateBetaLocalizationCommand>("testflight_localization_update", "Writes the beta app description, feedback e-mail and links of one language."),
        Write<SubmitBetaReviewCommand>("testflight_beta_review", "Submits a build to TestFlight beta app review. Needs confirm: true and confirmVersion equal to the build's \"version (build)\" text."),

        // ASO keywords
        Read<ResearchAsoKeywordsQuery>("aso_keywords_research", "Measures up to 20 search keywords for one app in one store and country (ISO code): estimated popularity 0 to 100 from the store's autocomplete, difficulty 0 to 100 from the top ten results' ratings, competing apps, top ten competitors, the app's current rank and an opportunity score. Scores are data based estimates, not AI. App Store and Google Play; not Microsoft Store."),
        Read<GetAsoKeywordSeedsQuery>("aso_keywords_seeds", "Keyword candidates for research from data: the app's listing (name, subtitle, keywords field, description terms), words shared by competitors' names and the store's search suggestions. Measure them with aso_keywords_research."),
        Write<SuggestAsoKeywordSeedsCommand>("aso_keywords_ideas", "Keyword ideas grouped by theme from the organization's AI key, as candidates only (no scores). Measure them with aso_keywords_research. Audited with tokens and cost."),
        Write<TrackAsoKeywordsCommand>("aso_keywords_track", "Starts or resumes daily rank tracking of keywords for one app, store and country, optionally with competitors' ranks. A keyword tracked before keeps its history. Limited per organization; Google Play only when the server enabled it."),
        Write<UntrackAsoKeywordsCommand>("aso_keywords_untrack", "Stops daily rank tracking of keywords by tracking id; their rank history is kept."),
        Read<GetAsoTrackedKeywordsQuery>("aso_keywords_tracked", "Tracked keywords of one app in one store: current rank, change over 1, 7 and 30 days, the last 30 days and the latest popularity and difficulty."),
        Read<GetAsoKeywordRanksQuery>("aso_keywords_ranks", "Daily rank history of one tracked keyword (null rank means not in the results read; unchecked days are marked), followed competitors' histories and the latest top ten."),
        Read<GetAsoKeywordCoverageQuery>("aso_keywords_coverage", "Apple keywords field helper per listing language: which tracked keywords the name, subtitle and keywords field cover, missing and repeated words, characters left of 100."),

        // Before and after impact of store changes
        Read<GetAsoImpactEventsQuery>("aso_impact_events", "Changes of one app that may have moved its store page metrics, newest first: applied change plans of the store page (listing texts with locales and fields, screenshots, icon, previews, price, category) and versions that went live, each with the UTC day it took effect (the store's live day when known; plans written to a version not live yet are marked awaiting release). Range: from/to or days (default 180)."),
        Read<GetAsoImpactQuery>("aso_impact_get", "Before and after report of one change (eventId from aso_impact_events): equal windows (windowDays 7, 14 or 28, default 14) before the effect day and after a gap (gapDays, default 2: the effect day and the next are skipped). Impressions, page views and downloads as daily averages over matching weekdays, conversion rate with a two proportion z-test (signal InsufficientData below 100 page views a window), tracked keyword ranks (average, top 10, per keyword change) and other changes inside the windows (confounders). Filters: country, source."),
        Read<GetAsoImpactRecentQuery>("aso_impact_recent", "The impact of the organization's recent changes (default the last 60 days, at most 50): page views, downloads and conversion rate before and after, the conversion signal and whether another change confounds it."),

        // Microsoft Store
        Write<AddMsProductCommand>("ms_product_add", "Adds an MSI/EXE product by its Store id (MSIX apps come with the sync)."),
        Read<GetMsProductDetailQuery>("ms_product_get", "Reads a Microsoft Store product: flags, active submission, Partner Center rules and assisted steps."),
        Read<GetMsContentQuery>("ms_content_get", "Reads the current Microsoft Store content of a product, flight or add-on (listings, images, packages, pricing, rollout)."),
        Plan<ApplyMsChangeCommand>("ms_change_plan", "Plans one Microsoft Store change (listings, images, MSIX packages of the app or a flight, pricing, installers, rollout, new flight or add-on, add-on content, discard a draft).", [], ["BaseHash"], []),
        Read<GetMsSubmissionListQuery>("ms_submissions_list", "Lists the Microsoft Store submissions storeasy knows for a product, with status history."),
        Read<GetMsFlightListQuery>("ms_flights_list", "Lists the package flights of an MSIX app."),
        Write<SaveMsFlightGroupCommand>("ms_flight_group_save", "Records the name of a Partner Center flight group (groups are made in Partner Center)."),
        Read<GetMsAddonListQuery>("ms_addons_list", "Lists the add-ons of an MSIX app."),
        Read<GetMsInstallerPackageListQuery>("ms_installer_packages", "Lists the installer packages of an MSI/EXE product with the Authenticode check result."),
        Write<CheckMsInstallerCommand>("ms_installer_check", "Queues the Authenticode check of an MSI/EXE product's installers."),

        // Subscriptions, in-app purchases and purchase settings
        Read<GetSubscriptionGroupsQuery>("subscriptions_groups", "Lists the subscription groups and subscriptions (App Store) or subscriptions and base plans (Play) of a store record."),
        Read<GetSubscriptionDetailQuery>("subscriptions_get", "Reads one subscription with its localizations, availability and review state."),
        Read<GetSubscriptionOffersQuery>("subscriptions_offers", "Lists the introductory, promotional and win-back offers of a subscription."),
        Read<GetSubscriptionPricesQuery>("subscriptions_prices", "Pages through the prices of a subscription per territory, with pending changes."),
        Read<GetSubscriptionPricePointsQuery>("subscriptions_price_points", "Lists the price points of a subscription in a territory."),
        Read<ConvertSubscriptionPricesQuery>("subscriptions_prices_convert", "Converts one price into every territory with the store's equalization."),
        Plan<PlanSubscriptionChangesCommand>("subscriptions_plan", "Plans subscription changes (groups, subscriptions, localizations, prices, offers, availability)."),
        Write<SyncStoreProductsCommand>("products_sync", "Reads the subscriptions and in-app purchases of a store record now (a store job)."),
        Read<GetIapListQuery>("iap_list", "Pages through the in-app purchases (one time products) of a store record."),
        Read<GetIapDetailQuery>("iap_get", "Reads one in-app purchase with its localizations, availability and review state."),
        Read<GetIapPricesQuery>("iap_prices", "Pages through the prices of an in-app purchase per territory."),
        Read<GetIapPricePointsQuery>("iap_price_points", "Lists the price points of an in-app purchase in a territory."),
        Plan<PlanIapChangesCommand>("iap_plan", "Plans in-app purchase changes (products, localizations, prices, availability)."),
        Read<GetSandboxTesterListQuery>("sandbox_testers_list", "Lists the App Store sandbox testers."),
        Plan<PlanSandboxTestersCommand>("sandbox_testers_plan", "Plans sandbox tester changes."),
        Read<GetServerNotificationsQuery>("server_notifications_get", "Reads the App Store server notification URLs and version."),
        Plan<PlanServerNotificationsCommand>("server_notifications_plan", "Plans the App Store server notification URLs (ManageWebhooks permission to apply)."),

        // Store listing experiments and custom product pages
        Read<GetAsoExperimentOverviewQuery>("aso_experiments_list", "Lists the store listing experiments and custom pages of an app (App Store product page optimization and custom product pages, Play experiments and custom listings) with their state, and what each store supports: App Store through the API, Play as an assisted step (the Play API has none), Microsoft Store not at all."),
        Read<GetAsoExperimentDetailQuery>("aso_experiment_get", "Reads one experiment or custom page record: variants (what each changes per locale), store state, dates, links (custom product page URL with ppid), timeline, the writes that can be planned now and, for Play, the Play Console checklist."),
        Write<SaveAsoExperimentCommand>("aso_experiment_save", "Creates (Id null) or edits a draft record: kind, name, traffic proportion, up to 3 treatments with an alternate icon name and screenshot/preview asset ids per locale (from uploads or mockups_export_manifest), or a custom page's promotional texts. Nothing is written to a store."),
        Plan<PlanAsoExperimentCommand>("aso_experiment_plan", "Plans an App Store experiment or custom product page write: Push (create or update with treatments and media), Submit (for App Review, alone or WithVersion), Start, Stop, ApplyWinner (writes the winner's media to the editable app version). Start, Stop and ApplyWinner need the production release permission, Submit the submit permission; all but Push need the typed confirmation (app name and experiment name)."),
        Write<RecordAsoExperimentResultCommand>("aso_experiment_result", "Records the result of an experiment (summary, winner, results link) and, for Play records run in Play Console, their state, start and end."),
        Write<UpdateAsoExperimentChecklistCommand>("aso_experiment_checklist", "Saves the done items of a Play record's Play Console checklist."),
        Read<GetAsoExperimentExportQuery>("aso_experiment_export", "Download links of a record's variant files and its localized texts, to set a Play experiment or custom listing up in Play Console."),
        Write<SyncAsoExperimentsCommand>("aso_experiments_sync", "Reads an App Store record's experiments and custom product pages now (a store job); ones made in App Store Connect get a record."),

        // Studio
        Read<GetMockupTemplateListQuery>("mockups_templates_list", "Lists the mockup templates of the organization."),
        Read<GetMockupTemplateDetailQuery>("mockups_template_get", "Reads one mockup template with its spec and captions."),
        Read<GetMockupStarterTemplatesQuery>("mockups_starter_templates", "Lists the starter templates a new template can start from."),
        Read<GetMockupFramesQuery>("mockups_frames", "Lists the device frames."),
        Write<CreateMockupTemplateCommand>("mockups_template_create", "Creates a mockup template (storeasy data)."),
        Write<UpdateMockupTemplateCommand>("mockups_template_update", "Updates a mockup template (storeasy data)."),
        Write<SaveMockupCaptionsCommand>("mockups_captions_save", "Saves the captions of a template per locale and slot."),
        Write<SuggestMockupCaptionsCommand>("mockups_captions_suggest", "Suggests caption translations for a template."),
        Read<RenderMockupPreviewQuery>("mockups_preview", "Renders a preview of a template slot (returns a short lived image URL)."),
        Read<GetMockupExportPlanQuery>("mockups_export_plan", "Shows what an export would render: classes, locales, sizes and image count."),
        Write<ExportMockupsCommand>("mockups_export", "Queues a mockup export job that renders and stores the images (no store write). Returns the job id."),
        Read<GetMockupExportManifestQuery>("mockups_export_manifest", "Reads the manifest of a finished export job (asset ids by store, class and locale)."),
    ];

    private static McpToolDefinition Read<TRequest>(string name, string description, params (string Property, object Value)[] fixedValues)
        => Define(typeof(TRequest), name, description, McpToolKind.Read, fixedValues, [], []);

    private static McpToolDefinition Write<TRequest>(string name, string description, params (string Property, object Value)[] fixedValues)
        => Define(typeof(TRequest), name, description, McpToolKind.Write, fixedValues, [], []);

    private static McpToolDefinition Plan<TRequest>(string name, string description)
        => Plan<TRequest>(name, description, [], [], []);

    private static McpToolDefinition Plan<TRequest>(string name,
                                                    string description,
                                                    (string Property, object Value)[] fixedValues,
                                                    string[] hidden,
                                                    (string Property, McpUploadSpec Spec)[] uploads)
    {
        return Define(typeof(TRequest), name, description + _planNote, McpToolKind.Plan, [.. fixedValues, ("DryRun", true)], [.. hidden, "Confirm"], uploads);
    }

    private static McpToolDefinition Define(Type type, string name, string description, McpToolKind kind, (string Property, object Value)[] fixedValues, string[] hidden, (string Property, McpUploadSpec Spec)[] uploads) => new()
    {
        Name = name,
        Description = description,
        Kind = kind,
        RequestType = type,
        Fixed = fixedValues.Where(f => type.GetProperty(f.Property) != null).ToDictionary(f => f.Property, f => f.Value),
        Hidden = [.. hidden.Where(h => type.GetProperty(h) != null)],
        Uploads = uploads.ToDictionary(u => u.Property, u => u.Spec),
    };
}
