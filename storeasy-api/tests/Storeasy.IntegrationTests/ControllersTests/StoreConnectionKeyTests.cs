using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Dtos.OrganizationDtos;
using Storeasy.Application.Dtos.StoreDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Domain.Enums;
using Storeasy.IntegrationTests.TestBase;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Replacing the key of an App Store Connect or Google Play connection in place: same team, verified first, old key kept on failure.")]
public class StoreConnectionKeyTests(StoreasyApiFactory factory) : IntegrationTestBase(factory)
{
    private const string _issuer = "57246542-96fe-1a63-e053-0824d011072a";

    private sealed record Setup(TestUser Owner, int OrganizationId, int ConnectionId);

    private static string AppleSecret(string keyId, string issuerId = _issuer)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        return JsonSerializer.Serialize(new { issuerId, keyId, privateKey = key.ExportPkcs8PrivateKeyPem() });
    }

    private static string GoogleSecret(string project)
    {
        using var key = RSA.Create(2048);

        return JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["type"] = "service_account",
            ["project_id"] = project,
            ["private_key"] = key.ExportPkcs8PrivateKeyPem(),
            ["client_email"] = $"store@{project}.iam.gserviceaccount.com",
        });
    }

    private static string PrivateKey(string secret) => JsonDocument.Parse(secret).RootElement.GetProperty("privateKey").GetString();

    private async Task<Setup> NewSetupAsync(StoreKind store, string secret, DateTime? credentialExpiryDate = null)
    {
        var owner = await _factory.CreateAndLoginUserAsync();
        var (_, organization) = await owner.Client.PostAsync<int>("organizations/organization", new { name = $"Org {TestApi.Unique()}" });
        var (http, connection) = await owner.Client.PostAsync<int>("storeconnections/connection", new { organizationId = organization.Data, store, name = "Team key", secret, credentialExpiryDate });

        http.StatusCode.Should().Be(HttpStatusCode.OK, connection?.Messages?.FirstOrDefault()?.Message);

        return new Setup(owner, organization.Data, connection.Data);
    }

    private Task<Domain.Stores.StoreConnection> RowAsync(int connectionId)
        => _factory.DbAsync(db => db.StoreConnections.AsNoTracking().SingleAsync(c => c.Id == connectionId));

    private async Task<TestUser> AddMemberAsync(Setup setup, OrgRole role)
    {
        var user = await _factory.CreateUserAsync(email: $"m{TestApi.Unique()}@example.com");
        var member = await _factory.LoginAsync(user.UserName, TestApi.DefaultPassword);
        var (_, invitation) = await setup.Owner.Client.PostAsync<InvitationCreatedDto>("organizations/invitation", new { organizationId = setup.OrganizationId, email = user.Email, role });

        await member.Client.PostAsync<int>("organizations/invitation/accept", new { token = invitation.Data.Token });

        return member;
    }

    [Fact]
    public async Task ReplaceKey_ForApple_ShouldSealTheVerifiedNewKeyInTheSameConnection_AndAuditItWithoutTheSecret()
    {
        // Arrange
        var setup = await NewSetupAsync(StoreKind.Apple, AppleSecret("ABC123DEFG"));
        var before = await RowAsync(setup.ConnectionId);
        var newSecret = AppleSecret("ADM1N2KEY3");

        // Act
        var (http, body) = await setup.Owner.Client.PutAsync<StoreConnectionDto>("storeconnections/connection/key", new { organizationId = setup.OrganizationId, connectionId = setup.ConnectionId, secret = newSecret });
        var replacedSecret = _factory.StoreGateway.VerifiedSecrets.Last();
        var (_, verified) = await setup.Owner.Client.PostAsync<StoreConnectionDto>("storeconnections/connection/verify", new { organizationId = setup.OrganizationId, connectionId = setup.ConnectionId });
        var reopenedSecret = _factory.StoreGateway.VerifiedSecrets.Last();
        var after = await RowAsync(setup.ConnectionId);
        var (_, logs) = await setup.Owner.Client.PatchListAsync<StoreAuditLogDto>("organizations/audit-logs", new { organizationId = setup.OrganizationId, pageNumber = 1, rowCount = 10 });

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Data.Id.Should().Be(setup.ConnectionId, "the key is replaced in place");
        body.Data.Fingerprint.KeyId.Should().Be("ADM1N2KEY3");
        body.Data.Fingerprint.IssuerIdSuffix.Should().Be("072a");
        body.Data.Status.Should().Be(ConnectionStatus.Verified);
        body.Data.LastVerifiedDate.Should().NotBeNull();
        JsonSerializer.Serialize(body).Should().NotContain("PRIVATE KEY");
        PrivateKey(replacedSecret).Should().Be(PrivateKey(newSecret), "the new key is verified before it is saved");
        verified.Data.Status.Should().Be(ConnectionStatus.Verified);
        PrivateKey(reopenedSecret).Should().Be(PrivateKey(newSecret), "the sealed secret now opens to the new key");
        after.EncryptedSecret.Should().NotEqual(before.EncryptedSecret);
        Encoding.UTF8.GetString(after.EncryptedSecret).Should().NotContain("PRIVATE KEY");
        var entry = logs.Data.Should().ContainSingle(l => l.Action == AuditAction.ConnectionKeyReplaced).Subject;
        entry.Summary.Should().Contain("ABC123DEFG").And.Contain("ADM1N2KEY3");
        entry.Details.Should().Contain("ADM1N2KEY3").And.NotContain("PRIVATE");
    }

    [Fact]
    public async Task ReplaceKey_ForApple_FromAnotherTeam_ShouldBeRefusedWithoutCallingTheStore()
    {
        // Arrange
        var setup = await NewSetupAsync(StoreKind.Apple, AppleSecret("ABC123DEFG"));
        var before = await RowAsync(setup.ConnectionId);
        var verifications = _factory.StoreGateway.VerifiedSecrets.Count;

        // Act
        var (http, body) = await setup.Owner.Client.PutAsync<StoreConnectionDto>("storeconnections/connection/key", new
        {
            organizationId = setup.OrganizationId,
            connectionId = setup.ConnectionId,
            secret = AppleSecret("OTHER12345", "11111111-2222-3333-4444-55555555072a"),
        });
        var after = await RowAsync(setup.ConnectionId);

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Messages.Should().Contain(m => m.Key == MessageKey.StoreKeyIssuerMismatch, "the full issuer id is compared, not only its last 4 characters");
        _factory.StoreGateway.VerifiedSecrets.Count.Should().Be(verifications);
        after.EncryptedSecret.Should().Equal(before.EncryptedSecret);
        after.Fingerprint.KeyId.Should().Be("ABC123DEFG");
    }

    [Fact]
    public async Task ReplaceKey_WhenTheStoreRejectsTheNewKey_ShouldKeepTheOldKeyAndStatus()
    {
        // Arrange
        var setup = await NewSetupAsync(StoreKind.Apple, AppleSecret("ABC123DEFG"));
        await setup.Owner.Client.PostAsync<StoreConnectionDto>("storeconnections/connection/verify", new { organizationId = setup.OrganizationId, connectionId = setup.ConnectionId });
        var before = await RowAsync(setup.ConnectionId);

        // Act
        _factory.StoreGateway.NextVerification = new StoreVerification(ConnectionStatus.Rejected, "401: NOT_AUTHORIZED");

        (HttpResponseMessage Http, Milvasoft.Components.Rest.MilvaResponse.Response<StoreConnectionDto> Body) result;

        try
        {
            result = await setup.Owner.Client.PutAsync<StoreConnectionDto>("storeconnections/connection/key", new { organizationId = setup.OrganizationId, connectionId = setup.ConnectionId, secret = AppleSecret("ADM1N2KEY3") });
        }
        finally
        {
            _factory.StoreGateway.NextVerification = new StoreVerification(ConnectionStatus.Verified, null);
        }

        var after = await RowAsync(setup.ConnectionId);

        // Assert
        result.Http.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.Body.Messages.Should().Contain(m => m.Key == MessageKey.StoreKeyRejected);
        result.Body.Messages.Should().Contain(m => m.Message.Contains("NOT_AUTHORIZED"), "the store's answer tells why");
        after.EncryptedSecret.Should().Equal(before.EncryptedSecret);
        after.EncryptedDataKey.Should().Equal(before.EncryptedDataKey);
        after.Fingerprint.KeyId.Should().Be("ABC123DEFG");
        after.Status.Should().Be(ConnectionStatus.Verified);
        after.LastVerifiedDate.Should().Be(before.LastVerifiedDate);
    }

    [Fact]
    public async Task ReplaceKey_ForGoogle_ShouldTakeAnotherServiceAccount()
    {
        // Arrange
        var setup = await NewSetupAsync(StoreKind.Google, GoogleSecret("demo"));
        var viewer = await AddMemberAsync(setup, OrgRole.Viewer);

        // Act
        var (viewerHttp, _) = await viewer.Client.PutAsync<StoreConnectionDto>("storeconnections/connection/key", new { organizationId = setup.OrganizationId, connectionId = setup.ConnectionId, secret = GoogleSecret("other") });
        var (malformedHttp, malformed) = await setup.Owner.Client.PutAsync<StoreConnectionDto>("storeconnections/connection/key", new { organizationId = setup.OrganizationId, connectionId = setup.ConnectionId, secret = "{\"type\":\"user\"}" });
        var (http, body) = await setup.Owner.Client.PutAsync<StoreConnectionDto>("storeconnections/connection/key", new { organizationId = setup.OrganizationId, connectionId = setup.ConnectionId, secret = GoogleSecret("other") });

        // Assert
        viewerHttp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        malformedHttp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        malformed.Messages.Should().Contain(m => m.Key == MessageKey.GoogleSecretInvalid);
        http.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Data.Id.Should().Be(setup.ConnectionId);
        body.Data.Fingerprint.ServiceAccountEmail.Should().Be("store@other.iam.gserviceaccount.com");
        body.Data.Fingerprint.ProjectId.Should().Be("other");
        body.Data.Status.Should().Be(ConnectionStatus.Verified);
    }

    [Fact]
    public async Task ReplaceKey_ForMicrosoftOrAnUnknownConnection_ShouldBeRefused()
    {
        // Arrange
        var secret = JsonSerializer.Serialize(new { tenantId = Guid.NewGuid().ToString(), clientId = "0f1e2d3c-4b5a-4697-8a8b-7c6d5e4fbe12", clientSecret = "client-secret-value", sellerId = "40291187" });
        var setup = await NewSetupAsync(StoreKind.Microsoft, secret, DateTime.UtcNow.AddMonths(12));

        // Act
        var (microsoftHttp, microsoft) = await setup.Owner.Client.PutAsync<StoreConnectionDto>("storeconnections/connection/key", new { organizationId = setup.OrganizationId, connectionId = setup.ConnectionId, secret });
        var (missingHttp, _) = await setup.Owner.Client.PutAsync<StoreConnectionDto>("storeconnections/connection/key", new { organizationId = setup.OrganizationId, connectionId = setup.ConnectionId + 100000, secret = AppleSecret("ABC123DEFG") });

        // Assert
        microsoftHttp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        microsoft.Messages.Should().Contain(m => m.Key == MessageKey.StoreKeyReplaceNotSupported);
        missingHttp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
