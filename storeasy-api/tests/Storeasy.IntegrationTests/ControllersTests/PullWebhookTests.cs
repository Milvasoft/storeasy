using FluentAssertions;
using Storeasy.Application.Dtos.IntakeDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Enums;
using Storeasy.IntegrationTests.TestBase;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Pull sources: EAS webhook (expo-signature) and the generic signed webhook, allowed download domains and redirects.")]
public class PullWebhookTests(StoreasyApiFactory factory) : IntakeTestBase(factory)
{
    private async Task<(Org Org, Record Google, string Package, ArtifactSourceSavedDto Source)> SourceAsync(ArtifactPullSourceKind kind)
    {
        var org = await NewOrgAsync();
        var package = $"com.{org.Suffix}.mola";

        _factory.Stores.Google.AddApp(package);

        var (_, google) = await NewAppAsync(org, null, package);

        await org.Owner.Client.PutAsync<int>("artifacts/delivery-rules", new
        {
            organizationId = org.OrganizationId,
            appId = google.AppId,
            rules = new[] { new { platform = ArtifactPlatform.Android, enabled = true, target = DeliveryTarget.PlayTrack, targetId = "internal", onWarning = DeliveryWarningAction.Deliver } },
        });

        var (http, source) = await org.Owner.Client.PutAsync<ArtifactSourceSavedDto>("artifacts/source", new
        {
            organizationId = org.OrganizationId,
            kind,
            appId = google.AppId,
            allowedDomains = new[] { "expo.dev", "*.expo.dev", "api.codemagic.io" },
            platforms = new[] { ArtifactPlatform.Android },
        });

        http.StatusCode.Should().Be(HttpStatusCode.OK, source?.MessagesToString());

        return (org, google, package, source.Data);
    }

    private async Task<(HttpStatusCode Status, ArtifactUploadDto Data)> PostAsync(string path, string body, params (string Name, string Value)[] headers)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path.TrimStart('/')) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        foreach (var (name, value) in headers)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        var http = await _factory.CreateClientWithAddress().SendAsync(request);
        var envelope = await TestApi.ReadAsync<Milvasoft.Components.Rest.MilvaResponse.Response<ArtifactUploadDto>>(http);

        return (http.StatusCode, envelope?.Data);
    }

    [Fact]
    public async Task EasBuild_ShouldBeDownloadedFromAnAllowedDomainAndDelivered()
    {
        var (org, _, package, source) = await SourceAsync(ArtifactPullSourceKind.Eas);
        var aab = PackageFactory.Aab(package, 88);
        var url = $"https://builds.expo.dev/artifacts/{TestApi.Unique()}/build.aab";

        _factory.Downloads.Serve(url, aab);

        var body = JsonSerializer.Serialize(new
        {
            id = "build-1",
            status = "finished",
            platform = "android",
            buildDetailsPageUrl = "https://expo.dev/accounts/kuzey/builds/1",
            artifacts = new { buildUrl = url },
            metadata = new { appName = "Mola", appVersion = "3.1.3", appBuildVersion = "88", distribution = "store" },
        });

        var (status, accepted) = await PostAsync(source.Url, body, ("expo-signature", PullWebhooks.SignEas(source.Secret, body)));
        var session = await WaitForSessionAsync(accepted.SessionId);
        var (badStatus, _) = await PostAsync(source.Url, body, ("expo-signature", PullWebhooks.SignEas("wrong-secret", body)));
        var ignoredBody = body.Replace("\"finished\"", "\"errored\"");
        var (ignoredStatus, ignored) = await PostAsync(source.Url, ignoredBody, ("expo-signature", PullWebhooks.SignEas(source.Secret, ignoredBody)));
        var foreignBody = body.Replace(url, "https://evil.example.com/build.aab");
        var (foreignStatus, _) = await PostAsync(source.Url, foreignBody, ("expo-signature", PullWebhooks.SignEas(source.Secret, foreignBody)));

        source.Secret.Should().StartWith("whsec_");
        status.Should().Be(HttpStatusCode.Accepted);
        session.Status.Should().Be(ArtifactSessionStatus.Delivered, session.StatusDescription);
        session.Source.Should().Be(ArtifactSource.Eas);
        session.Sha256.Should().Be(PackageFactory.Sha256(aab));
        session.DownloadUrl.Should().BeNull();
        badStatus.Should().Be(HttpStatusCode.Unauthorized);
        ignoredStatus.Should().Be(HttpStatusCode.OK);
        ignored.Should().BeNull();
        foreignStatus.Should().Be(HttpStatusCode.UnprocessableEntity);
        _factory.Downloads.Requests.Should().NotContain(r => r.Contains("evil.example.com"));
        _ = org;
    }

    [Fact]
    public async Task GenericWebhook_ShouldCheckTheTimestampTheSignatureAndTheChecksum()
    {
        var (_, google, package, source) = await SourceAsync(ArtifactPullSourceKind.Webhook);
        var aab = PackageFactory.Aab(package, 91);
        var url = $"https://api.codemagic.io/artifacts/{TestApi.Unique()}/app.aab";
        var redirected = $"https://builds.expo.dev/redirect/{TestApi.Unique()}/app.aab";

        _factory.Downloads.Serve(url, aab);
        _factory.Downloads.Redirect(redirected, "https://internal.evil.test/app.aab");

        string Body(string downloadUrl, string sha256) => JsonSerializer.Serialize(new
        {
            schema = PullWebhooks.ArtifactReadySchema,
            appId = google.AppId,
            platform = "android",
            fileName = "app.aab",
            downloadUrl,
            sha256,
            size = aab.Length,
            source = new { provider = "codemagic", runId = "6738f1c2a4" },
        });

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var old = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var good = Body(url, PackageFactory.Sha256(aab));
        var wrongSha = Body(url.Replace("app.aab", "other.aab"), PackageFactory.Sha256([1, 2, 3]));
        var redirect = Body(redirected, PackageFactory.Sha256([4, 5]));

        _factory.Downloads.Serve(url.Replace("app.aab", "other.aab"), PackageFactory.Aab(package, 92));

        var (status, accepted) = await PostAsync(source.Url, good, ("X-Storeasy-Timestamp", now), ("X-Storeasy-Signature", PullWebhooks.SignArtifact(source.Secret, good, now)));
        var delivered = await WaitForSessionAsync(accepted.SessionId);
        var (duplicateStatus, duplicate) = await PostAsync(source.Url, good, ("X-Storeasy-Timestamp", now), ("X-Storeasy-Signature", PullWebhooks.SignArtifact(source.Secret, good, now)));
        var (oldStatus, _) = await PostAsync(source.Url, good, ("X-Storeasy-Timestamp", old), ("X-Storeasy-Signature", PullWebhooks.SignArtifact(source.Secret, good, old)));
        var (_, mismatch) = await PostAsync(source.Url, wrongSha, ("X-Storeasy-Timestamp", now), ("X-Storeasy-Signature", PullWebhooks.SignArtifact(source.Secret, wrongSha, now)));
        var rejected = await WaitForSessionAsync(mismatch.SessionId);
        var (_, redirectAccepted) = await PostAsync(source.Url, redirect, ("X-Storeasy-Timestamp", now), ("X-Storeasy-Signature", PullWebhooks.SignArtifact(source.Secret, redirect, now)));
        var redirectSession = await WaitForSessionAsync(redirectAccepted.SessionId);
        var (schemaStatus, _) = await PostAsync(source.Url, "{\"schema\":\"other\"}", ("X-Storeasy-Timestamp", now), ("X-Storeasy-Signature", PullWebhooks.SignArtifact(source.Secret, "{\"schema\":\"other\"}", now)));

        status.Should().Be(HttpStatusCode.Accepted);
        delivered.Status.Should().Be(ArtifactSessionStatus.Delivered, delivered.StatusDescription);
        duplicateStatus.Should().Be(HttpStatusCode.OK);
        duplicate.Duplicate.Should().BeTrue();
        oldStatus.Should().Be(HttpStatusCode.Unauthorized);
        rejected.Status.Should().Be(ArtifactSessionStatus.Rejected);
        rejected.CiSummary.Should().Contain("SHA-256");
        redirectSession.Status.Should().Be(ArtifactSessionStatus.Rejected, "a redirect to a domain that is not allowed is refused");
        _factory.Downloads.Requests.Should().NotContain(r => r.Contains("internal.evil.test"));
        schemaStatus.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
