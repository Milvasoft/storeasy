using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Dtos.AppDtos;
using Storeasy.Application.Dtos.IntakeDtos;
using Storeasy.Application.Dtos.TokenDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Intake;
using Storeasy.IntegrationTests.TestBase;
using Storeasy.IntegrationTests.TestBase.Fakes;
using System.Net;
using System.Text.Json.Nodes;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Package intake: CI tokens, OIDC exchange, upload sessions (resume, duplicate, complete), parsing, checks, delivery rules and delivery to Play and TestFlight.")]
public class ArtifactIntakeTests(StoreasyApiFactory factory) : IntakeTestBase(factory)
{
    private FakeGooglePlay Play => _factory.Stores.Google;

    private async Task<(Org Org, Record Google, string Package)> AndroidAppAsync(string track = "internal", DeliveryWarningAction onWarning = DeliveryWarningAction.Hold)
    {
        var org = await NewOrgAsync();
        var package = $"com.{org.Suffix}.halka";

        Play.AddApp(package);

        var (_, google) = await NewAppAsync(org, null, package);

        if (track != null)
        {
            var (http, body) = await org.Owner.Client.PutAsync<int>("artifacts/delivery-rules", new
            {
                organizationId = org.OrganizationId,
                appId = google.AppId,
                rules = new[] { new { platform = ArtifactPlatform.Android, enabled = true, target = DeliveryTarget.PlayTrack, targetId = track, targetName = track, onWarning } },
            });

            http.StatusCode.Should().Be(HttpStatusCode.OK, body?.MessagesToString());
        }

        return (org, google, package);
    }

    [Fact]
    public async Task AndroidBundle_ShouldBeParsedCheckedAndDeliveredToTheRuleTrack()
    {
        var (org, google, package) = await AndroidAppAsync();
        var client = TokenClient(await PatAsync(org.Owner, org.OrganizationId));
        var aab = PackageFactory.Aab(package, 42);

        var (sessionId, open, complete) = await UploadAsync(client, google.AppId, "halka-1.0.0-42.aab", aab);
        var session = await WaitForSessionAsync(sessionId);
        var (_, detail) = await client.GetAsync<ArtifactSessionDetailDto>($"artifacts/session?SessionId={sessionId}");

        open.Resumed.Should().BeFalse();
        open.PartSize.Should().Be(IntakeRules.DefaultPartSize);
        complete.Data.Status.Should().BeOneOf(ArtifactSessionStatus.Parsing, ArtifactSessionStatus.Delivered, ArtifactSessionStatus.Checking, ArtifactSessionStatus.Delivering);
        session.Status.Should().Be(ArtifactSessionStatus.Delivered, session.StatusDescription);
        session.CiExitCode.Should().Be(0);
        detail.Data.Package.Identifier.Should().Be(package);
        detail.Data.Package.VersionCode.Should().Be("42");
        detail.Data.Package.TargetSdk.Should().Be("36");
        detail.Data.Checks.Should().NotBeEmpty().And.OnlyContain(c => c.Status == ReadinessStatus.Ok);
        detail.Data.Steps.Should().HaveCount(6).And.OnlyContain(s => s.Status == ArtifactStepStatus.Done);
        detail.Data.AppliedRule.TargetId.Should().Be("internal");
        detail.Data.CiResult.ExitCode.Should().Be(0);
        detail.Data.Identity.Kind.Should().Be(IdentityKind.User);
        Play.Apps[package]["bundles"].AsArray().Should().ContainSingle();
        Play.Apps[package]["tracks"]["internal"]["releases"].ToJsonString().Should().Contain("100");
        Play.OpenEdits.Should().Be(0);
    }

    [Fact]
    public async Task SameFile_ShouldBeADuplicateAndAnOpenSessionShouldResume()
    {
        var (org, google, package) = await AndroidAppAsync(track: null);
        var client = TokenClient(await PatAsync(org.Owner, org.OrganizationId));
        var aab = PackageFactory.Aab(package, 7);
        var sha256 = PackageFactory.Sha256(aab);

        var (_, first) = await client.PostAsync<ArtifactUploadDto>($"apps/{google.AppId}/artifacts", new { fileName = "a.aab", size = aab.Length, sha256 });
        PutParts(first.Data, aab);
        var (_, resumed) = await client.PostAsync<ArtifactUploadDto>($"apps/{google.AppId}/artifacts", new { fileName = "a.aab", size = aab.Length, sha256 });
        var (incompleteHttp, incomplete) = await client.PutAsync<ArtifactSessionListDto>($"artifacts/{first.Data.SessionId}/complete", new { parts = Array.Empty<object>() });
        var (_, complete) = await client.PutAsync<ArtifactSessionListDto>($"artifacts/{first.Data.SessionId}/complete", new { parts = resumed.Data.UploadedParts.Select(p => new { partNumber = p.PartNumber, eTag = p.ETag }) });
        var (_, again) = await client.PutAsync<ArtifactSessionListDto>($"artifacts/{first.Data.SessionId}/complete", new { parts = resumed.Data.UploadedParts.Select(p => new { partNumber = p.PartNumber, eTag = p.ETag }) });
        await WaitForSessionAsync(first.Data.SessionId);
        var (_, duplicate) = await client.PostAsync<ArtifactUploadDto>($"apps/{google.AppId}/artifacts", new { fileName = "a.aab", size = aab.Length, sha256 });
        var (_, duplicateDetail) = await client.GetAsync<ArtifactSessionDetailDto>($"artifacts/session?SessionId={duplicate.Data.SessionId}");

        resumed.Data.SessionId.Should().Be(first.Data.SessionId);
        resumed.Data.Resumed.Should().BeTrue();
        resumed.Data.Parts.Should().BeEmpty();
        resumed.Data.UploadedParts.Should().ContainSingle();
        incompleteHttp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Keys(incomplete).Should().Contain(MessageKey.UploadIncomplete);
        complete.IsSuccess.Should().BeTrue();
        again.IsSuccess.Should().BeTrue("complete is idempotent");
        duplicate.Data.Duplicate.Should().BeTrue();
        duplicate.Data.OriginalSessionId.Should().Be(first.Data.SessionId);
        duplicate.Data.Parts.Should().BeEmpty();
        duplicateDetail.Data.Status.Should().Be(ArtifactSessionStatus.Duplicate);
        duplicateDetail.Data.CiResult.ExitCode.Should().Be(0);
        (await _factory.DbAsync(db => db.StoreJobs.CountAsync(j => j.OrganizationId == org.OrganizationId && j.Kind == StoreJobKind.ProcessArtifact))).Should().Be(1);
    }

    [Fact]
    public async Task WrongPackage_LowVersionCode_AndProductionTarget_ShouldBeRefused()
    {
        var (org, google, package) = await AndroidAppAsync();
        var client = TokenClient(await PatAsync(org.Owner, org.OrganizationId));

        await _factory.DbAsync(async db =>
        {
            db.Builds.Add(new Build { OrganizationId = org.OrganizationId, StoreAppId = google.StoreAppId, Store = StoreKind.Google, ExternalId = "50", BuildNumber = "50", Version = "1.0", SyncedDate = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });

        var (wrongId, _, _) = await UploadAsync(client, google.AppId, "dev.aab", PackageFactory.Aab(package + ".dev", 60));
        var (lowId, _, _) = await UploadAsync(client, google.AppId, "low.aab", PackageFactory.Aab(package, 49));
        var aab = PackageFactory.Aab(package, 61);
        var (productionHttp, production) = await client.PostAsync<ArtifactUploadDto>($"apps/{google.AppId}/artifacts", new { fileName = "p.aab", size = aab.Length, sha256 = PackageFactory.Sha256(aab), delivery = new { store = StoreKind.Google, track = "production" } });
        var (mismatchHttp, mismatch) = await client.PostAsync<ArtifactUploadDto>($"apps/{google.AppId}/artifacts", new { fileName = "p.aab", size = aab.Length, sha256 = PackageFactory.Sha256(aab), delivery = new { store = StoreKind.Apple } });
        var (typeHttp, _) = await client.PostAsync<ArtifactUploadDto>($"apps/{google.AppId}/artifacts", new { fileName = "p.zip", size = 10, sha256 = PackageFactory.Sha256([1]) });

        var wrong = await WaitForSessionAsync(wrongId);
        var low = await WaitForSessionAsync(lowId);

        wrong.Status.Should().Be(ArtifactSessionStatus.Rejected);
        wrong.CiExitCode.Should().Be(2);
        wrong.CiSummary.Should().Contain(package + ".dev");
        low.Status.Should().Be(ArtifactSessionStatus.Rejected);
        low.Checks.Should().Contain(c => c.Key == "version-code" && c.Status == ReadinessStatus.Missing);
        productionHttp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Keys(production).Should().Contain(MessageKey.DeliveryTargetNotAllowed);
        mismatchHttp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Keys(mismatch).Should().Contain(MessageKey.DeliveryTargetNotAllowed);
        typeHttp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Play.Apps[package]["bundles"].AsArray().Should().BeEmpty("nothing reaches the store");
    }

    [Fact]
    public async Task Warning_ShouldHoldTheDeliveryUntilAMemberDeliversIt()
    {
        var (org, google, package) = await AndroidAppAsync();
        var client = TokenClient(await PatAsync(org.Owner, org.OrganizationId));

        var (sessionId, _, _) = await UploadAsync(client, google.AppId, "w.aab", PackageFactory.Aab(package, 70, targetSdk: null));
        var held = await WaitForSessionAsync(sessionId);
        var viewer = await MemberAsync(org, OrgRole.Viewer);
        var (viewerHttp, _) = await viewer.Client.PostAsync<ArtifactSessionDetailDto>("artifacts/session/deliver", new { organizationId = org.OrganizationId, sessionId });
        var (deliverHttp, _) = await org.Owner.Client.PostAsync<ArtifactSessionDetailDto>("artifacts/session/deliver", new { organizationId = org.OrganizationId, sessionId });
        var delivered = await WaitForSessionAsync(sessionId);

        held.Status.Should().Be(ArtifactSessionStatus.Held);
        held.CiExitCode.Should().Be(0);
        held.HoldReason.Should().Contain("warnings");
        viewerHttp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        deliverHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        delivered.Status.Should().Be(ArtifactSessionStatus.Delivered);
    }

    [Fact]
    public async Task HoldForConfirmation_ShouldShowTheChecksBeforeAnyDelivery()
    {
        var (org, google, package) = await AndroidAppAsync(onWarning: DeliveryWarningAction.Deliver);

        var client = TokenClient(await PatAsync(org.Owner, org.OrganizationId));
        var (sessionId, _, _) = await UploadAsync(client, google.AppId, "c.aab", PackageFactory.Aab(package, 80), new { holdForConfirmation = true });
        var held = await WaitForSessionAsync(sessionId);
        var bundlesWhileHeld = Play.Apps[package]["bundles"].AsArray().Count;
        var (deliverHttp, _) = await org.Owner.Client.PostAsync<ArtifactSessionDetailDto>("artifacts/session/deliver", new { organizationId = org.OrganizationId, sessionId });
        var delivered = await WaitForSessionAsync(sessionId);

        held.Status.Should().Be(ArtifactSessionStatus.Held);
        held.HoldReason.Should().StartWith("Waiting for confirmation");
        held.Checks.Should().NotBeEmpty("the package was read and checked before it was held");
        bundlesWhileHeld.Should().Be(0);
        deliverHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        delivered.Status.Should().Be(ArtifactSessionStatus.Delivered);
    }

    [Fact]
    public async Task Counts_ShouldCountThePeriodsPackagesByStatusAndSource()
    {
        var (org, google, package) = await AndroidAppAsync();
        var (other, _, _) = await AndroidAppAsync();

        var client = TokenClient(await PatAsync(org.Owner, org.OrganizationId));
        var (deliveredId, _, _) = await UploadAsync(client, google.AppId, "a.aab", PackageFactory.Aab(package, 90));
        await WaitForSessionAsync(deliveredId);
        var (heldId, _, _) = await UploadAsync(client, google.AppId, "b.aab", PackageFactory.Aab(package, 91, targetSdk: null));
        await WaitForSessionAsync(heldId);

        var (http, counts) = await org.Owner.Client.GetAsync<ArtifactCountsDto>($"artifacts/counts?organizationId={org.OrganizationId}");
        var (_, appCounts) = await org.Owner.Client.GetAsync<ArtifactCountsDto>($"artifacts/counts?organizationId={org.OrganizationId}&appId={int.MaxValue}");
        var (foreign, _) = await other.Owner.Client.GetAsync<ArtifactCountsDto>($"artifacts/counts?organizationId={org.OrganizationId}");
        var (tooLong, _) = await org.Owner.Client.GetAsync<ArtifactCountsDto>($"artifacts/counts?organizationId={org.OrganizationId}&from=2026-01-01T00:00:00Z&to=2027-06-01T00:00:00Z");

        http.StatusCode.Should().Be(HttpStatusCode.OK);
        counts.Data.Total.Should().Be(2);
        counts.Data.ByStatus.Single(s => s.Status == ArtifactSessionStatus.Delivered).Count.Should().Be(1);
        counts.Data.ByStatus.Single(s => s.Status == ArtifactSessionStatus.Held).Count.Should().Be(1);
        counts.Data.ByStatus.Should().HaveCount(Enum.GetValues<ArtifactSessionStatus>().Length);
        counts.Data.BySource.Should().ContainSingle().Which.Count.Should().Be(2);
        appCounts.Data.Total.Should().Be(0);
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
        tooLong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CiToken_ShouldUploadToItsAppsOnly()
    {
        var (org, google, package) = await AndroidAppAsync();
        var (_, otherApp) = await NewAppAsync(org, null, $"com.{org.Suffix}.other");
        var editor = await MemberAsync(org, OrgRole.Editor);
        var (editorHttp, _) = await editor.Client.PostAsync<CiTokenCreatedDto>("artifacts/ci-token", new { organizationId = org.OrganizationId, name = "nope", appIds = new[] { google.AppId }, expiryDays = 30 });
        var token = await CiTokenAsync(org.Owner, org.OrganizationId, google.AppId);
        var client = TokenClient(token);
        var aab = PackageFactory.Aab(package, 80);

        var (_, current) = await client.GetAsync<CurrentTokenDto>("tokens/current");
        var (otherHttp, other) = await client.PostAsync<ArtifactUploadDto>($"apps/{otherApp.AppId}/artifacts", new { fileName = "x.aab", size = aab.Length, sha256 = PackageFactory.Sha256(aab) });
        var (appsHttp, _) = await client.PatchListAsync<AppListDto>("apps", new { pageNumber = 1 });
        var (sessionId, _, _) = await UploadAsync(client, google.AppId, "ci.aab", aab);
        var session = await WaitForSessionAsync(sessionId);

        await _factory.DbAsync(db => db.CiTokens.Where(t => t.OrganizationId == org.OrganizationId).ExecuteUpdateAsync(s => s.SetProperty(t => t.ExpiryDate, DateTime.UtcNow.AddMinutes(-1))));
        var (expiredHttp, expired) = await client.GetAsync<CurrentTokenDto>("tokens/current");

        editorHttp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        current.Data.Kind.Should().Be(TokenKind.CiToken);
        current.Data.Scopes.Should().Equal(CiToken.ArtifactsWriteScope);
        current.Data.AppIds.Should().Equal(google.AppId);
        current.Data.UserName.Should().BeNull();
        otherHttp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        Keys(other).Should().Contain(MessageKey.AppForbidden);
        appsHttp.StatusCode.Should().Be(HttpStatusCode.Forbidden, "CI tokens only reach the intake endpoints");
        session.Status.Should().Be(ArtifactSessionStatus.Delivered);
        session.IdentityKind.Should().Be(IdentityKind.CiToken);
        session.CiTokenPrefix.Should().Be(token[..7]);
        expiredHttp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        Keys(expired).Should().Contain(MessageKey.CiTokenExpired);
    }

    [Fact]
    public async Task OidcExchange_ShouldIssueAnUploadTokenOnlyForAMatchingRule()
    {
        var (org, google, package) = await AndroidAppAsync();
        var (_, otherApp) = await NewAppAsync(org, null, $"com.{org.Suffix}.other");
        var repository = $"kuzeylab/{org.Suffix}";
        var (ruleHttp, rule) = await org.Owner.Client.PostAsync<int>("artifacts/trusted-publisher", new { organizationId = org.OrganizationId, appId = google.AppId, provider = OidcProvider.GitHub, repository, branch = "release/*", environment = "play-internal" });
        var anonymous = _factory.CreateClientWithAddress();
        var issuers = _factory.OidcIssuers;

        var good = issuers.CreateToken(FakeOidcIssuers.GitHub, "storeasy", FakeOidcIssuers.GitHubClaims(repository, "release/1.2", "play-internal"));
        var (okHttp, ok) = await anonymous.PostAsync<OidcExchangeDto>("oidc/exchange", new { appId = google.AppId, token = good, provider = 1 });
        var stored = await _factory.DbAsync(db => db.TrustedPublishers.AsNoTracking().SingleAsync(r => r.Id == rule.Data));
        var (branchHttp, branch) = await anonymous.PostAsync<OidcExchangeDto>("oidc/exchange", new { appId = google.AppId, token = issuers.CreateToken(FakeOidcIssuers.GitHub, "storeasy", FakeOidcIssuers.GitHubClaims(repository, "feature/x", "play-internal")) });
        var (audienceHttp, audience) = await anonymous.PostAsync<OidcExchangeDto>("oidc/exchange", new { appId = google.AppId, token = issuers.CreateToken(FakeOidcIssuers.GitHub, "api://AzureADTokenExchange", FakeOidcIssuers.GitHubClaims(repository, "release/1", "play-internal")) });
        var (expiredHttp, _) = await anonymous.PostAsync<OidcExchangeDto>("oidc/exchange", new { appId = google.AppId, token = issuers.CreateToken(FakeOidcIssuers.GitHub, "storeasy", FakeOidcIssuers.GitHubClaims(repository, "release/1", "play-internal"), TimeSpan.FromMinutes(-10)) });
        var (signatureHttp, _) = await anonymous.PostAsync<OidcExchangeDto>("oidc/exchange", new { appId = google.AppId, token = issuers.CreateToken(FakeOidcIssuers.GitHub, "storeasy", FakeOidcIssuers.GitHubClaims(repository, "release/1", "play-internal"), foreignKey: true) });
        var (issuerHttp, _) = await anonymous.PostAsync<OidcExchangeDto>("oidc/exchange", new { appId = google.AppId, token = issuers.CreateToken("https://evil.example.com", "storeasy", FakeOidcIssuers.GitHubClaims(repository, "release/1", "play-internal")) });
        var (noRuleHttp, noRule) = await anonymous.PostAsync<OidcExchangeDto>("oidc/exchange", new { appId = otherApp.AppId, token = good });
        var (missingHttp, _) = await anonymous.PostAsync<OidcExchangeDto>("oidc/exchange", new { appId = google.AppId });

        var upload = TokenClient(ok.Data.UploadToken);
        var (_, current) = await upload.GetAsync<CurrentTokenDto>("tokens/current");
        var aab = PackageFactory.Aab(package, 90);
        var (otherHttp, _) = await upload.PostAsync<ArtifactUploadDto>($"apps/{otherApp.AppId}/artifacts", new { fileName = "x.aab", size = aab.Length, sha256 = PackageFactory.Sha256(aab) });
        var (sessionId, _, _) = await UploadAsync(upload, google.AppId, "oidc.aab", aab);
        var session = await WaitForSessionAsync(sessionId);
        var last = await _factory.DbAsync(db => db.TrustedPublishers.AsNoTracking().SingleAsync(r => r.Id == rule.Data));

        ruleHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        okHttp.StatusCode.Should().Be(HttpStatusCode.OK, ok?.MessagesToString());
        ok.Data.UploadToken.Should().StartWith("sut_");
        ok.Data.ExpiryDate.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromMinutes(1));
        branchHttp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        Keys(branch).Should().Contain(MessageKey.OidcClaimMismatch);
        branch.Messages.Should().NotContain(m => m.Message.Contains("feature/x"), "the response never names the claim");
        audienceHttp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        Keys(audience).Should().Contain(MessageKey.OidcTokenInvalid);
        expiredHttp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        signatureHttp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        issuerHttp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        noRuleHttp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        Keys(noRule).Should().Contain(MessageKey.AppNotFound);
        missingHttp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        current.Data.Kind.Should().Be(TokenKind.UploadToken);
        current.Data.AppIds.Should().Equal(google.AppId);
        otherHttp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        session.Status.Should().Be(ArtifactSessionStatus.Delivered);
        session.IdentityKind.Should().Be(IdentityKind.Oidc);
        session.TrustedPublisherId.Should().Be(rule.Data);
        stored.LastExchangeResult.Should().Be(OidcExchangeResult.Succeeded);
        last.LastExchangeResult.Should().Be(OidcExchangeResult.InvalidToken, "the last attempt was a token that failed validation");
        stored.LastClaims.Should().Contain(c => c.Name == "repository");
        stored.LastClaims.Should().NotContain(c => c.Value.StartsWith("eyJ"), "the raw token is never stored");
        issuers.KeySetDownloads.Should().BeLessThan(4, "the key set is cached");
    }

    [Fact]
    public async Task AzureDevOpsRule_ShouldAcceptTheWorkloadIdentityAudienceAndServiceConnection()
    {
        var (org, google, _) = await AndroidAppAsync();
        await org.Owner.Client.PostAsync<int>("artifacts/trusted-publisher", new { organizationId = org.OrganizationId, appId = google.AppId, provider = OidcProvider.AzureDevOps, organization = "kuzeylab", project = "Pusula", serviceConnection = "storeasy-pusula" });
        var anonymous = _factory.CreateClientWithAddress();

        var ok = _factory.OidcIssuers.CreateToken(FakeOidcIssuers.Azure, "api://AzureADTokenExchange", new Dictionary<string, object> { ["sub"] = "sc://kuzeylab/Pusula/storeasy-pusula" });
        var wrong = _factory.OidcIssuers.CreateToken(FakeOidcIssuers.Azure, "api://AzureADTokenExchange", new Dictionary<string, object> { ["sub"] = "sc://other/Pusula/storeasy-pusula" });

        var (okHttp, okBody) = await anonymous.PostAsync<OidcExchangeDto>("oidc/exchange", new { appId = google.AppId, token = ok, provider = 2 });
        var (wrongHttp, _) = await anonymous.PostAsync<OidcExchangeDto>("oidc/exchange", new { appId = google.AppId, token = wrong, provider = 2 });

        okHttp.StatusCode.Should().Be(HttpStatusCode.OK, okBody?.MessagesToString());
        wrongHttp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task IosPackage_ShouldBeUploadedWithBuildUploadsAndAddedToTheInternalGroup()
    {
        var org = await NewOrgAsync();
        var appleId = _factory.Stores.Apple.NewId("app");
        _factory.Stores.Apple.AddApp(appleId);
        var (apple, _) = await NewAppAsync(org, appleId);
        var bundleId = $"com.{org.Suffix}.pusula";
        _factory.StoreEvents.TestFlight[appleId] = new TestFlightSnapshot([new BetaGroupRecord("grp-internal", "Kuzey ici", true, false, null, null, false, []), new BetaGroupRecord("grp-external", "Public", false, true, null, null, false, [])], [], [], []);

        await org.Owner.Client.PutAsync<int>("artifacts/delivery-rules", new
        {
            organizationId = org.OrganizationId,
            appId = apple.AppId,
            rules = new[] { new { platform = ArtifactPlatform.Ios, enabled = true, target = DeliveryTarget.TestFlightGroup, targetId = "grp-internal", targetName = "Kuzey ici", onWarning = DeliveryWarningAction.Deliver } },
        });

        var client = TokenClient(await PatAsync(org.Owner, org.OrganizationId));
        var ipa = PackageFactory.Ipa(bundleId, "2.4", "119");

        var (sessionId, _, _) = await UploadAsync(client, apple.AppId, "pusula-2.4-119.ipa", ipa, delivery: new { notes = "Login fixes" });
        var session = await WaitForSessionAsync(sessionId);

        var (externalId, _, _) = await UploadAsync(client, apple.AppId, "pusula-2.4-120.ipa", PackageFactory.Ipa(bundleId, "2.4", "120"), delivery: new { group = "Public" });
        var external = await WaitForSessionAsync(externalId);
        var (devId, _, _) = await UploadAsync(client, apple.AppId, "dev.ipa", PackageFactory.Ipa(bundleId, "2.4", "121", development: true));
        var dev = await WaitForSessionAsync(devId);

        session.Status.Should().Be(ArtifactSessionStatus.Delivered, session.StatusDescription);
        session.Package.XcodeVersion.Should().Be("26.1");
        session.StoreBuildId.Should().NotBeNullOrEmpty();
        _factory.Stores.Apple.BuildUploads.Values.Should().Contain(u => (string)u["app"] == appleId && (string)u["buildNumber"] == "119" && (string)u["state"] == "COMPLETE");
        _factory.Stores.Apple.ExportCompliance[session.StoreBuildId].Should().BeFalse();
        _factory.StoreEvents.Changes.Should().Contain(c => c.AppId == appleId && c.Change.Operation == TestFlightOperation.UpdateBuilds && c.Change.GroupExternalId == "grp-internal" && c.Change.AddBuildExternalIds.Contains(session.StoreBuildId));
        _factory.StoreEvents.Changes.Should().Contain(c => c.AppId == appleId && c.Change.Operation == TestFlightOperation.WhatToTest && c.Change.Text == "Login fixes");
        external.Status.Should().Be(ArtifactSessionStatus.Failed, "an external group needs beta review and is never a delivery target");
        external.CiExitCode.Should().Be(4);
        dev.Status.Should().Be(ArtifactSessionStatus.Rejected);
        dev.Checks.Should().Contain(c => c.Key == "signature" && c.Status == ReadinessStatus.Missing);
    }

    [Fact]
    public async Task Sessions_ShouldBeIsolatedByOrganization()
    {
        var (org, google, package) = await AndroidAppAsync(track: null);
        var other = await NewOrgAsync();
        var mine = TokenClient(await PatAsync(org.Owner, org.OrganizationId));
        var theirs = TokenClient(await PatAsync(other.Owner, other.OrganizationId));
        var theirCi = TokenClient(await CiTokenAsync(other.Owner, other.OrganizationId));

        var (sessionId, _, _) = await UploadAsync(mine, google.AppId, "iso.aab", PackageFactory.Aab(package, 5));
        await WaitForSessionAsync(sessionId);
        var aab = PackageFactory.Aab(package, 6);

        var (readHttp, read) = await theirs.GetAsync<ArtifactSessionDetailDto>($"artifacts/session?SessionId={sessionId}");
        var (ciReadHttp, _) = await theirCi.GetAsync<ArtifactSessionDetailDto>($"artifacts/session?SessionId={sessionId}");
        var (openHttp, _) = await theirCi.PostAsync<ArtifactUploadDto>($"apps/{google.AppId}/artifacts", new { fileName = "x.aab", size = aab.Length, sha256 = PackageFactory.Sha256(aab) });
        var (completeHttp, _) = await theirs.PutAsync<ArtifactSessionListDto>($"artifacts/{sessionId}/complete", new { parts = Array.Empty<object>() });
        var (listHttp, list) = await theirs.PatchListAsync<ArtifactSessionListDto>("artifacts/sessions", new { pageNumber = 1 });
        var (rulesHttp, _) = await other.Owner.Client.GetAsync<DeliveryRulesDto>($"artifacts/delivery-rules?OrganizationId={other.OrganizationId}&AppId={google.AppId}");

        readHttp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        Keys(read).Should().Contain(MessageKey.ArtifactSessionNotFound);
        ciReadHttp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        openHttp.StatusCode.Should().Be(HttpStatusCode.NotFound, "another organization's app does not exist for the token");
        completeHttp.StatusCode.Should().Be(HttpStatusCode.NotFound);
        listHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        list.Data.Should().NotContain(s => s.Id == sessionId);
        rulesHttp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RetentionCleanup_ShouldDeleteOldPackagesAbandonedUploadsAndPendingAssets()
    {
        var (org, google, package) = await AndroidAppAsync(track: null);
        var client = TokenClient(await PatAsync(org.Owner, org.OrganizationId));
        var (sessionId, _, _) = await UploadAsync(client, google.AppId, "old.aab", PackageFactory.Aab(package, 3));
        await WaitForSessionAsync(sessionId);
        var aab = PackageFactory.Aab(package, 4);
        var (_, abandoned) = await client.PostAsync<ArtifactUploadDto>($"apps/{google.AppId}/artifacts", new { fileName = "abandoned.aab", size = aab.Length, sha256 = PackageFactory.Sha256(aab) });
        var pending = new Storeasy.Domain.Assets.Asset { OrganizationId = org.OrganizationId, Purpose = AssetPurpose.Screenshot, ObjectKey = $"org/{org.OrganizationId}/test/{Guid.NewGuid():N}/p.png", FileName = "p.png", ContentType = "image/png", Size = 10, Status = AssetStatus.Pending };

        await _factory.DbAsync(async db =>
        {
            db.Assets.Add(pending);
            await db.SaveChangesAsync();
            await db.Assets.Where(a => a.Id == pending.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.CreationDate, DateTime.UtcNow.AddDays(-3)));
            await db.ArtifactSessions.Where(s => s.Id == sessionId).ExecuteUpdateAsync(s => s.SetProperty(x => x.StartDate, DateTime.UtcNow.AddDays(-31)));
            await db.ArtifactSessions.Where(s => s.Id == abandoned.Data.SessionId).ExecuteUpdateAsync(s => s.SetProperty(x => x.StartDate, DateTime.UtcNow.AddDays(-3)));
        });

        var orphanKey = $"org/{org.OrganizationId}/artifacts/orphan";
        _factory.FileStorage.AddOpenUpload(orphanKey, "orphan-upload", DateTime.UtcNow.AddDays(-5));

        await _factory.WithServiceAsync<Storeasy.Infrastructure.Intake.IntakeCleaner, int>(cleaner => cleaner.RunAsync(CancellationToken.None));

        var old = await _factory.DbAsync(db => db.ArtifactSessions.AsNoTracking().SingleAsync(s => s.Id == sessionId));
        var closed = await _factory.DbAsync(db => db.ArtifactSessions.AsNoTracking().SingleAsync(s => s.Id == abandoned.Data.SessionId));
        var pendingLeft = await _factory.DbAsync(db => db.Assets.AnyAsync(a => a.Id == pending.Id));

        old.PackageDeleted.Should().BeTrue();
        _factory.FileStorage.Deleted.Should().Contain(old.ObjectKey);
        closed.Status.Should().Be(ArtifactSessionStatus.Failed);
        closed.UploadId.Should().BeNull();
        pendingLeft.Should().BeFalse();
        _factory.FileStorage.Aborted.Should().Contain(a => a.UploadId == "orphan-upload");
    }

    [Fact]
    public async Task IntakeSettings_ShouldLimitThePackageSize()
    {
        var (org, google, package) = await AndroidAppAsync(track: null);
        await org.Owner.Client.PutAsync<int>("artifacts/settings", new { organizationId = org.OrganizationId, artifactRetentionDays = 7, maxArtifactBytes = 5L * 1024 * 1024 });
        var client = TokenClient(await PatAsync(org.Owner, org.OrganizationId));

        var (http, body) = await client.PostAsync<ArtifactUploadDto>($"apps/{google.AppId}/artifacts", new { fileName = "big.aab", size = 6L * 1024 * 1024, sha256 = PackageFactory.Sha256([9]) });
        var (_, settings) = await client.GetAsync<IntakeSettingsDto>("artifacts/settings");

        http.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Keys(body).Should().Contain(MessageKey.ArtifactTooLarge);
        settings.Data.ArtifactRetentionDays.Should().Be(7);
        _ = package;
    }
}
