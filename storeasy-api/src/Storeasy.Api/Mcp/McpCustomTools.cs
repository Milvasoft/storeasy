using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Features.ChangePlans.ApplyChangePlan;
using Storeasy.Application.Features.ChangePlans.GetChangePlanDetail;
using Storeasy.Application.Features.Media.PlanMediaSet;
using Storeasy.Application.Features.Mockups.GetMockupExportManifest;
using Storeasy.Application.Features.Iap.ApplyIapPlan;
using Storeasy.Application.Features.MsStore.SubmitMsSubmission;
using Storeasy.Application.Features.Subscriptions.ApplySubscriptionPlan;
using Storeasy.Application.Features.Registries.ApproveRegistryPlan;
using Storeasy.Application.Features.Releases.SubmitForReview;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Domain;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Storeasy.Api.Mcp;

/// <summary>
/// Tools that compose requests: approving a plan or a registry plan (with the confirmation rules), submitting for review, a screenshot folder
/// upload and turning a studio export into screenshot plans.
/// </summary>
public static class McpCustomTools
{
    /// <summary>
    /// The composed tools.
    /// </summary>
    public static IReadOnlyList<McpToolDefinition> All { get; } =
    [
        new()
        {
            Name = "plans_approve",
            Kind = McpToolKind.Write,
            Description = "Approves a pending change plan and queues the store write, with the same checks as the web: the plan's own permission "
                          + "(production releases ReleaseProduction, submissions SubmitForReview) and, for plans that need it, confirm: true plus "
                          + "confirmText typed exactly as the plan's confirmText (app name and version). The store is read again first; a changed store refuses the approval.",
            Schema = Schema(("planId", Integer("Plan id.")), ("confirm", Boolean("Must be true for review submission and production plans.")), ("confirmText", Text("Typed confirmation: the plan's confirmText."))).Required("planId"),
            Handler = ApprovePlanAsync,
        },
        new()
        {
            Name = "submit_for_review",
            Kind = McpToolKind.Write,
            Description = "Submits the App Store version for review (SubmitForReview permission). Needs confirm: true and confirmText typed exactly "
                          + "like the web (the app name and the version, as readiness_check and plans_get show it). Never automatic.",
            Schema = Schema(("storeAppId", Integer("App Store record.")), ("versionId", Text("App Store version id; empty for the editable version.")),
                            ("confirm", Boolean("Must be true.")), ("confirmText", Text("App name and version, typed exactly."))).Required("storeAppId", "confirm", "confirmText"),
            Handler = SubmitForReviewAsync,
        },
        new()
        {
            Name = "screenshots_upload_dir",
            Kind = McpToolKind.Plan,
            Description = "Plans one screenshot set from a folder of images (the local storeasy-mcp bridge uploads the folder and sends asset ids). "
                          + "The images replace the set in file name order." + " Returns a change plan; nothing is written until plans_approve.",
            Schema = Schema(("storeAppId", Integer("Store record (App Store or Play).")),
                            ("locale", Text("Locale, e.g. en-US.")),
                            ("displayType", Text("Screenshot class: App Store display type (APP_IPHONE_67), Play image type (phoneScreenshots) or the catalog class id.")),
                            ("screenshots", Upload("Screenshots in display order.", "folder", 1, ".png", ".jpg", ".jpeg"))).Required("storeAppId", "locale", "displayType", "screenshots"),
            Handler = UploadScreenshotFolderAsync,
        },
        new()
        {
            Name = "mockups_export_to_screenshot_plans",
            Kind = McpToolKind.Plan,
            Description = "Turns a finished studio export (job id) into screenshot plans of one store record: one plan per class and locale of that store, "
                          + "images in slot order. Returns the plans; nothing is written until plans_approve.",
            Schema = Schema(("jobId", Integer("Mockup export job id.")), ("storeAppId", Integer("Store record the screenshots go to."))).Required("jobId", "storeAppId"),
            Handler = PlanExportAsync,
        },
        new()
        {
            Name = "ms_submit",
            Kind = McpToolKind.Write,
            Description = "Sends a Microsoft Store draft (app, flight, add-on or MSI/EXE) to certification (SubmitForReview permission). Needs confirm: true "
                          + "and confirmText typed exactly like the web: the product's Store id. Never automatic.",
            Schema = Schema(("storeAppId", Integer("Microsoft Store record.")),
                            ("target", Text("app, flight, addon or installer.")),
                            ("flightId", Text("Flight of a flight submission.")),
                            ("addonStoreId", Text("Add-on of an add-on submission.")),
                            ("confirm", Boolean("Must be true.")),
                            ("confirmText", Text("The product's Store id, typed exactly."))).Required("storeAppId", "confirm", "confirmText"),
            Handler = SubmitMicrosoftAsync,
        },
        new()
        {
            Name = "subscriptions_apply",
            Kind = McpToolKind.Write,
            Description = "Applies a pending subscription plan. Same checks as plans_approve: the plan's own permission and, for plans that need it, confirm: true plus confirmText.",
            Schema = Schema(("planId", Integer("Plan id.")), ("confirm", Boolean("Must be true for plans that need a typed confirmation.")), ("confirmText", Text("The plan's confirmText, typed exactly."))).Required("planId"),
            Handler = (arguments, services, cancellationToken) => ApproveWithAsync(arguments, services, (planId, confirm) => new ApplySubscriptionPlanCommand { PlanId = planId, Confirm = confirm }, cancellationToken),
        },
        new()
        {
            Name = "iap_apply",
            Kind = McpToolKind.Write,
            Description = "Applies a pending in-app purchase plan. Same checks as plans_approve: the plan's own permission and, for plans that need it, confirm: true plus confirmText.",
            Schema = Schema(("planId", Integer("Plan id.")), ("confirm", Boolean("Must be true for plans that need a typed confirmation.")), ("confirmText", Text("The plan's confirmText, typed exactly."))).Required("planId"),
            Handler = (arguments, services, cancellationToken) => ApproveWithAsync(arguments, services, (planId, confirm) => new ApplyIapPlanCommand { PlanId = planId, Confirm = confirm }, cancellationToken),
        },
        new()
        {
            Name = "registries_plan_approve",
            Kind = McpToolKind.Write,
            Description = "Approves a pending registry plan (a release or a registry operation) and queues it, with the same checks as the web: the "
                          + "ReleaseProduction permission, confirm: true and confirmText typed exactly as the plan's confirmText (package or group name and "
                          + "version). A release plan whose version files changed on the branch since the plan is refused; plan it again. Never automatic.",
            Schema = Schema(("planId", Integer("Registry plan id.")), ("confirm", Boolean("Must be true.")), ("confirmText", Text("The plan's confirmText, typed exactly."))).Required("planId", "confirm", "confirmText"),
            Handler = ApproveRegistryPlanAsync,
            FeatureFlag = FeatureFlagKeys.Registries,
        },
    ];

    private static Task<object> ApprovePlanAsync(JsonObject arguments, IServiceProvider services, CancellationToken cancellationToken)
        => ApproveWithAsync(arguments, services, (planId, confirm) => new ApplyChangePlanCommand { PlanId = planId, Confirm = confirm }, cancellationToken);

    private static async Task<object> ApproveWithAsync(JsonObject arguments, IServiceProvider services, Func<long, string, object> approval, CancellationToken cancellationToken)
    {
        var planId = arguments["planId"]?.GetValue<long>() ?? 0;
        var detail = (Response<ChangePlanDto>)await McpToolInvoker.SendAsync(services, new GetChangePlanDetailQuery { PlanId = planId }, cancellationToken);

        if (!detail.IsSuccess)
            return detail;

        var confirmText = arguments["confirmText"]?.GetValue<string>();

        if (detail.Data.ConfirmRequired && arguments["confirm"]?.GetValue<bool>() != true)
            return McpToolInvoker.Failure(services, MessageKey.McpConfirmRequired, detail.Data.ConfirmText);

        return await McpToolInvoker.SendAsync(services, approval(planId, confirmText), cancellationToken);
    }

    private static async Task<object> ApproveRegistryPlanAsync(JsonObject arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        if (arguments["confirm"]?.GetValue<bool>() != true)
            return McpToolInvoker.Failure(services, MessageKey.McpConfirmRequired, "<package or group name> <version>");

        return await McpToolInvoker.SendAsync(services, new ApproveRegistryPlanCommand
        {
            PlanId = arguments["planId"]?.GetValue<long>() ?? 0,
            Confirm = arguments["confirmText"]?.GetValue<string>(),
        }, cancellationToken);
    }

    private static async Task<object> SubmitForReviewAsync(JsonObject arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        if (arguments["confirm"]?.GetValue<bool>() != true)
            return McpToolInvoker.Failure(services, MessageKey.McpConfirmRequired, "<app name> <version>");

        return await McpToolInvoker.SendAsync(services, new SubmitForReviewCommand
        {
            StoreAppId = arguments["storeAppId"]?.GetValue<int>() ?? 0,
            VersionId = arguments["versionId"]?.GetValue<string>(),
            Confirm = arguments["confirmText"]?.GetValue<string>(),
        }, cancellationToken);
    }

    private static async Task<object> SubmitMicrosoftAsync(JsonObject arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        if (arguments["confirm"]?.GetValue<bool>() != true)
            return McpToolInvoker.Failure(services, MessageKey.McpConfirmRequired, "<Store id>");

        var target = arguments["target"]?.GetValue<string>() is { Length: > 0 } text && Enum.TryParse<MsSubmissionTarget>(text, true, out var parsed) ? parsed : MsSubmissionTarget.App;

        return await McpToolInvoker.SendAsync(services, new SubmitMsSubmissionCommand
        {
            StoreAppId = arguments["storeAppId"]?.GetValue<int>() ?? 0,
            Target = target,
            FlightId = arguments["flightId"]?.GetValue<string>(),
            AddonStoreId = arguments["addonStoreId"]?.GetValue<string>(),
            Confirm = arguments["confirmText"]?.GetValue<string>(),
        }, cancellationToken);
    }

    private static Task<object> UploadScreenshotFolderAsync(JsonObject arguments, IServiceProvider services, CancellationToken cancellationToken)
        => McpToolInvoker.SendAsync(services, new PlanMediaSetCommand
        {
            StoreAppId = arguments["storeAppId"]?.GetValue<int>() ?? 0,
            Kind = MediaKind.Screenshot,
            Locale = arguments["locale"]?.GetValue<string>(),
            ClassKey = arguments["displayType"]?.GetValue<string>(),
            AssetIds = arguments["screenshots"]?.Deserialize<List<long>>(McpSchemaBuilder.Json) ?? [],
            DryRun = true,
        }, cancellationToken);

    private static async Task<object> PlanExportAsync(JsonObject arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        var storeAppId = arguments["storeAppId"]?.GetValue<int>() ?? 0;
        var manifest = (Response<MockupExportManifest>)await McpToolInvoker.SendAsync(services, new GetMockupExportManifestQuery { JobId = arguments["jobId"]?.GetValue<long>() ?? 0 }, cancellationToken);

        if (!manifest.IsSuccess)
            return manifest;

        var organizationId = services.GetRequiredService<ICurrentToken>().OrganizationId ?? 0;
        var record = await services.GetRequiredService<IStoreContentService>().GetStoreAppAsync(organizationId, storeAppId, cancellationToken);

        if (record == null)
            return McpToolInvoker.Failure(services, MessageKey.StoreAppNotFound);

        var plans = new List<object>();

        foreach (var set in manifest.Data.Sets.Where(s => s.Store == record.Store && s.Images.Count > 0))
        {
            plans.Add(await McpToolInvoker.SendAsync(services, new PlanMediaSetCommand
            {
                StoreAppId = storeAppId,
                Kind = MediaKind.Screenshot,
                Locale = set.Locale,
                ClassKey = set.StoreClassKey ?? set.ClassKey,
                AssetIds = [.. set.Images.OrderBy(i => i.SlotOrder).Select(i => i.AssetId)],
                DryRun = true,
            }, cancellationToken));
        }

        return Response<List<object>>.Success(plans);
    }

    private static JsonObject Schema(params (string Name, JsonObject Property)[] properties)
    {
        var props = new JsonObject();

        foreach (var (name, property) in properties)
        {
            props[name] = property;
        }

        return new JsonObject { ["type"] = "object", ["properties"] = props };
    }

    private static JsonObject Required(this JsonObject schema, params string[] names)
    {
        schema["required"] = new JsonArray([.. names.Select(n => (JsonNode)n)]);

        return schema;
    }

    private static JsonObject Integer(string description) => new() { ["type"] = "integer", ["description"] = description };

    private static JsonObject Boolean(string description) => new() { ["type"] = "boolean", ["description"] = description };

    private static JsonObject Text(string description) => new() { ["type"] = "string", ["description"] = description };

    private static JsonObject Upload(string description, string kind, int purpose, params string[] accept) => new()
    {
        ["type"] = "array",
        ["items"] = new JsonObject { ["type"] = "integer" },
        ["description"] = description,
        [McpSchemaBuilder.UploadKeyword] = new JsonObject
        {
            ["kind"] = kind,
            ["purpose"] = purpose,
            ["accept"] = new JsonArray([.. accept.Select(a => (JsonNode)a)]),
        },
    };
}
