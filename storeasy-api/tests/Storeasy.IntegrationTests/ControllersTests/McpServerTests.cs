using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Storeasy.Application.Utils.Constants;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Organizations;
using Storeasy.IntegrationTests.TestBase;
using System.Net;
using System.Text.Json;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Remote MCP at /mcp: personal access token authentication, tool list and schemas, role checks, plans and the confirmation rules, audit channel.")]
public class McpServerTests(StoreasyApiFactory factory) : IntakeTestBase(factory)
{
    private async Task<McpClient> ConnectAsync(string token)
    {
        var http = _factory.CreateClient();
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(http.BaseAddress, "/mcp"),
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
        }, http, NullLoggerFactory.Instance, ownsHttpClient: true);

        return await McpClient.CreateAsync(transport, cancellationToken: TestContext.Current.CancellationToken);
    }

    private static string Text(CallToolResult result) => string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text));

    [Fact]
    public async Task Tools_ShouldBeListedWithUploadArgumentsAndNoOrganizationArgument()
    {
        var org = await NewOrgAsync();
        await using var client = await ConnectAsync(await PatAsync(org.Owner, org.OrganizationId));

        var tools = await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);
        var names = tools.Select(t => t.Name).ToList();
        var upload = tools.Single(t => t.Name == "screenshots_upload_dir").ProtocolTool.InputSchema;

        names.Should().Contain(["apps_summary", "readiness_check", "listings_plan", "plans_approve", "submit_for_review", "screenshots_upload_dir", "jobs_get", "connections_list",
                                "reviews_reply_draft", "rejections_paste_text", "testflight_beta_review", "mockups_export_to_screenshot_plans", "artifacts_session_get"]);
        names.Should().NotContain("artifact_upload", "the local bridge owns it");
        names.Should().OnlyHaveUniqueItems();
        upload.GetProperty("properties").GetProperty("screenshots").GetProperty("x-storeasy-upload").GetProperty("kind").GetString().Should().Be("folder");
        tools.Single(t => t.Name == "bundle_upload_plan").ProtocolTool.InputSchema.GetProperty("properties").GetProperty("assetId").GetProperty("x-storeasy-upload").GetProperty("purpose").GetInt32().Should().Be(4);
        tools.Where(t => t.ProtocolTool.InputSchema.GetProperty("properties").TryGetProperty("organizationId", out var unused)).Should().BeEmpty();
        tools.Single(t => t.Name == "listings_plan").ProtocolTool.InputSchema.GetProperty("properties").TryGetProperty("dryRun", out _).Should().BeFalse("plan tools always make a dry run");
        tools.Should().OnlyContain(t => !t.Description.Contains((char)0x2014) && !t.Description.Contains((char)0x2013));
    }

    [Fact]
    public async Task OnlyAnActivePersonalAccessToken_ShouldOpenMcp()
    {
        var org = await NewOrgAsync();
        var ci = await CiTokenAsync(org.Owner, org.OrganizationId);
        var http = _factory.CreateClientWithAddress();

        foreach (var bearer in new[] { org.Owner.Token.AccessToken, ci, "sea_not_a_real_token_0000000000" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = new StringContent("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}""", System.Text.Encoding.UTF8, "application/json") };

            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearer);
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.Accept.ParseAdd("text/event-stream");

            var response = await http.SendAsync(request, TestContext.Current.CancellationToken);

            response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        }
    }

    [Fact]
    public async Task ToolCalls_ShouldRunWithTheMembersRoleAndBeAuditedOnTheMcpChannel()
    {
        var org = await NewOrgAsync();
        var package = $"com.{org.Suffix}.pusula";
        _factory.Stores.Google.AddApp(package);
        var (_, google) = await NewAppAsync(org, null, package);
        var viewer = await MemberAsync(org, OrgRole.Viewer);
        await using var owner = await ConnectAsync(await PatAsync(org.Owner, org.OrganizationId));
        await using var readOnly = await ConnectAsync(await PatAsync(viewer, org.OrganizationId, readOnly: true));
        var ct = TestContext.Current.CancellationToken;

        var summary = await owner.CallToolAsync("apps_summary", new Dictionary<string, object>(), cancellationToken: ct);
        var plan = await owner.CallToolAsync("listings_plan", new Dictionary<string, object>
        {
            ["storeAppId"] = google.StoreAppId,
            ["changes"] = new[] { new { locale = "en-US", field = "shortDescription", value = "Never get lost" } },
        }, cancellationToken: ct);
        var denied = await readOnly.CallToolAsync("listings_plan", new Dictionary<string, object>
        {
            ["storeAppId"] = google.StoreAppId,
            ["changes"] = new[] { new { locale = "en-US", field = "title", value = "x" } },
        }, cancellationToken: ct);
        var read = await readOnly.CallToolAsync("listings_get", new Dictionary<string, object> { ["storeAppId"] = google.StoreAppId }, cancellationToken: ct);

        var planId = JsonDocument.Parse(Text(plan)).RootElement.GetProperty("data").GetProperty("planId").GetInt64();
        var stored = await _factory.DbAsync(db => db.ChangePlans.AsNoTracking().SingleAsync(p => p.Id == planId));
        var approved = await owner.CallToolAsync("plans_approve", new Dictionary<string, object> { ["planId"] = planId }, cancellationToken: ct);
        var decision = await _factory.DbAsync(db => db.ChangePlans.AsNoTracking().SingleAsync(p => p.Id == planId));
        var audit = await _factory.DbAsync(db => db.StoreAuditLogs.AsNoTracking().Where(a => a.OrganizationId == org.OrganizationId && a.Action == AuditAction.PlanApproved).ToListAsync());

        summary.IsError.Should().NotBe(true, Text(summary));
        plan.IsError.Should().NotBe(true, Text(plan));
        stored.Status.Should().Be(ChangePlanStatus.Pending, "a plan tool never writes");
        stored.Source.Should().Be(ActionChannel.Mcp);
        denied.IsError.Should().BeTrue();
        Text(denied).Should().Contain(MessageKey.OrganizationForbidden);
        read.IsError.Should().NotBe(true, Text(read));
        approved.IsError.Should().NotBe(true, Text(approved));
        decision.DecisionChannel.Should().Be(ActionChannel.Mcp);
        audit.Should().ContainSingle().Which.Channel.Should().Be(ActionChannel.Mcp);
    }

    [Fact]
    public async Task ProductionPlans_AndReviewSubmission_ShouldNeedConfirmTrueAndTheTypedText()
    {
        var org = await NewOrgAsync();
        var package = $"com.{org.Suffix}.pusula";
        _factory.Stores.Google.AddApp(package);
        var (_, google) = await NewAppAsync(org, null, package);
        var editor = await MemberAsync(org, OrgRole.Editor);
        await using var owner = await ConnectAsync(await PatAsync(org.Owner, org.OrganizationId));
        await using var editorClient = await ConnectAsync(await PatAsync(editor, org.OrganizationId));
        var ct = TestContext.Current.CancellationToken;

        var plan = await owner.CallToolAsync("releases_plan", new Dictionary<string, object>
        {
            ["storeAppId"] = google.StoreAppId,
            ["kind"] = "assignTrack",
            ["track"] = "production",
            ["name"] = "2.5.0",
            ["versionCodes"] = new[] { "10" },
            ["status"] = "completed",
            ["releaseNotes"] = new[] { new { language = "en-US", text = "Fixes." } },
        }, cancellationToken: ct);
        var root = JsonDocument.Parse(Text(plan)).RootElement.GetProperty("data");
        var planId = root.GetProperty("planId").GetInt64();
        var confirmText = root.GetProperty("confirmText").GetString();

        var withoutConfirm = await owner.CallToolAsync("plans_approve", new Dictionary<string, object> { ["planId"] = planId, ["confirmText"] = confirmText }, cancellationToken: ct);
        var asEditor = await editorClient.CallToolAsync("plans_approve", new Dictionary<string, object> { ["planId"] = planId, ["confirm"] = true, ["confirmText"] = confirmText }, cancellationToken: ct);
        var wrongText = await owner.CallToolAsync("plans_approve", new Dictionary<string, object> { ["planId"] = planId, ["confirm"] = true, ["confirmText"] = "nope" }, cancellationToken: ct);
        var submit = await owner.CallToolAsync("submit_for_review", new Dictionary<string, object> { ["storeAppId"] = google.StoreAppId, ["confirm"] = false, ["confirmText"] = "x" }, cancellationToken: ct);
        var status = await _factory.DbAsync(db => db.ChangePlans.AsNoTracking().Where(p => p.Id == planId).Select(p => p.Status).SingleAsync());

        plan.IsError.Should().NotBe(true, Text(plan));
        confirmText.Should().NotBeNullOrEmpty();
        withoutConfirm.IsError.Should().BeTrue();
        Text(withoutConfirm).Should().Contain(MessageKey.McpConfirmRequired);
        asEditor.IsError.Should().BeTrue("production needs ReleaseProduction, which an editor lacks");
        wrongText.IsError.Should().BeTrue();
        Text(wrongText).Should().Contain(MessageKey.PlanConfirmMismatch);
        submit.IsError.Should().BeTrue();
        status.Should().Be(ChangePlanStatus.Pending);
    }
}
