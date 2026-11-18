using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Dtos.AppDtos;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Dtos.TokenDtos;
using Storeasy.Application.Utils.Constants;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Organizations;
using Storeasy.IntegrationTests.TestBase;
using System.Net;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Personal access tokens: creation, the token authentication, effective role, revocation, expiry and organization binding.")]
public class TokensControllerTests(StoreasyApiFactory factory) : IntakeTestBase(factory)
{
    [Fact]
    public async Task CreatedToken_ShouldBeShownOnceAndListedByPrefixOnly()
    {
        var org = await NewOrgAsync();

        var (_, created) = await org.Owner.Client.PostAsync<PersonalAccessTokenCreatedDto>("tokens/token", new { organizationId = org.OrganizationId, name = "Claude Code laptop", readOnly = false, expiryDays = 90 });
        var (_, list) = await org.Owner.Client.GetAsync<List<PersonalAccessTokenDto>>("tokens");
        var row = await _factory.DbAsync(db => db.PersonalAccessTokens.AsNoTracking().SingleAsync(t => t.Id == created.Data.Id));

        created.Data.Token.Should().StartWith("sea_");
        created.Data.Prefix.Should().Be(created.Data.Token[..7]);
        row.TokenHash.Should().NotBe(created.Data.Token).And.HaveLength(64);
        list.Data.Should().ContainSingle(t => t.Id == created.Data.Id).Which.Prefix.Should().Be(created.Data.Prefix);
        System.Text.Json.JsonSerializer.Serialize(list.Data).Should().NotContain(created.Data.Token);
    }

    [Fact]
    public async Task Token_ShouldDescribeItselfAndActInItsOrganizationOnly()
    {
        var org = await NewOrgAsync();
        var other = await NewOrgAsync();
        await NewAppAsync(org, null, $"com.{org.Suffix}.halka");
        await NewAppAsync(other, null, $"com.{other.Suffix}.halka");
        var token = await PatAsync(org.Owner, org.OrganizationId);
        var client = TokenClient(token);

        var (_, current) = await client.GetAsync<CurrentTokenDto>("tokens/current");
        var (http, apps) = await client.PatchListAsync<AppListDto>("apps", new { pageNumber = 1, rowCount = 100 });
        var (_, foreign) = await client.PatchListAsync<AppListDto>("apps", new { organizationId = other.OrganizationId, pageNumber = 1, rowCount = 100 });

        current.Data.Kind.Should().Be(TokenKind.PersonalAccessToken);
        current.Data.OrganizationId.Should().Be(org.OrganizationId);
        current.Data.UserName.Should().Be(org.Owner.UserName);
        http.StatusCode.Should().Be(HttpStatusCode.OK);
        apps.Data.Should().ContainSingle();
        foreign.Data.Should().ContainSingle().Which.Id.Should().Be(apps.Data[0].Id, "the organization id in the body is replaced by the token's");
    }

    [Fact]
    public async Task ReadOnlyToken_ShouldReadButNeverWrite()
    {
        var org = await NewOrgAsync();
        var package = $"com.{org.Suffix}.pusula";
        _factory.Stores.Google.AddApp(package);
        var (_, google) = await NewAppAsync(org, null, package);
        var client = TokenClient(await PatAsync(org.Owner, org.OrganizationId, readOnly: true));

        var (readHttp, _) = await client.PatchListAsync<AppListDto>("apps", new { pageNumber = 1, rowCount = 10 });
        var (writeHttp, write) = await client.PostAsync<ChangePlanDto>("listings/plan", new { storeAppId = google.StoreAppId, changes = new[] { new { locale = "en-US", field = "title", value = "x" } } });

        readHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        writeHttp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        Keys(write).Should().Contain(MessageKey.OrganizationForbidden);
    }

    [Fact]
    public async Task Viewer_ShouldCreateReadOnlyTokensOnly()
    {
        var org = await NewOrgAsync();
        var viewer = await MemberAsync(org, OrgRole.Viewer);

        var (writeHttp, _) = await viewer.Client.PostAsync<PersonalAccessTokenCreatedDto>("tokens/token", new { organizationId = org.OrganizationId, name = "write", readOnly = false, expiryDays = 30 });
        var (readHttp, _) = await viewer.Client.PostAsync<PersonalAccessTokenCreatedDto>("tokens/token", new { organizationId = org.OrganizationId, name = "read", readOnly = true, expiryDays = 30 });

        writeHttp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        readHttp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RoleChanges_ShouldApplyToTheTokenAtOnce()
    {
        var org = await NewOrgAsync();
        var editor = await MemberAsync(org, OrgRole.Editor);
        var token = await PatAsync(editor, org.OrganizationId);
        var client = TokenClient(token);

        await _factory.DbAsync(db => db.OrganizationMembers.Where(m => m.UserId == editor.Id && m.OrganizationId == org.OrganizationId).ExecuteDeleteAsync());
        await _factory.FlushRedisAsync();

        var (http, body) = await client.GetAsync<CurrentTokenDto>("tokens/current");

        http.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "a removed member's token stops working");
        Keys(body).Should().Contain(MessageKey.Unauthorized);
    }

    [Fact]
    public async Task RevokedAndExpiredTokens_ShouldAnswer401WithTheirKey()
    {
        var org = await NewOrgAsync();
        var revoked = await PatAsync(org.Owner, org.OrganizationId);
        var expired = await PatAsync(org.Owner, org.OrganizationId);
        var (_, list) = await org.Owner.Client.GetAsync<List<PersonalAccessTokenDto>>("tokens");
        var revokedId = list.Data.Single(t => revoked.StartsWith(t.Prefix, StringComparison.Ordinal) && t.Prefix == revoked[..7]).Id;

        var (revokeHttp, _) = await SendAsync<int>(org.Owner.Client, HttpMethod.Delete, "tokens/token", new { tokenId = revokedId });
        await _factory.DbAsync(db => db.PersonalAccessTokens.Where(t => t.OrganizationId == org.OrganizationId && t.Id != revokedId).ExecuteUpdateAsync(s => s.SetProperty(t => t.ExpiryDate, DateTime.UtcNow.AddMinutes(-1))));

        var (revokedHttp, revokedBody) = await TokenClient(revoked).GetAsync<CurrentTokenDto>("tokens/current");
        var (expiredHttp, expiredBody) = await TokenClient(expired).GetAsync<CurrentTokenDto>("tokens/current");
        var (unknownHttp, _) = await TokenClient("sea_unknown_token_value_0123456789").GetAsync<CurrentTokenDto>("tokens/current");

        revokeHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        revokedHttp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        Keys(revokedBody).Should().Contain(MessageKey.TokenRevoked);
        expiredHttp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        Keys(expiredBody).Should().Contain(MessageKey.TokenExpired);
        unknownHttp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_ShouldNotCreateTokensAndShouldRecordItsLastUse()
    {
        var org = await NewOrgAsync();
        var token = await PatAsync(org.Owner, org.OrganizationId);
        var client = TokenClient(token);

        client.DefaultRequestHeaders.UserAgent.ParseAdd("storeasy-cli/1.0.0");

        var (createHttp, _) = await client.PostAsync<PersonalAccessTokenCreatedDto>("tokens/token", new { organizationId = org.OrganizationId, name = "nested", expiryDays = 30 });
        await client.GetAsync<CurrentTokenDto>("tokens/current");
        await client.GetAsync<CurrentTokenDto>("tokens/current");

        var prefix = token[..7];
        var row = await _factory.DbAsync(db => db.PersonalAccessTokens.AsNoTracking().SingleAsync(t => t.Prefix == prefix && t.OrganizationId == org.OrganizationId));

        createHttp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        row.LastUseDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        row.LastClientName.Should().StartWith("storeasy-cli/1.0.0");
    }

    [Fact]
    public async Task PersonalTokenWrite_ShouldBeAuditedOnTheApiChannel()
    {
        var org = await NewOrgAsync();
        var client = TokenClient(await PatAsync(org.Owner, org.OrganizationId));

        var (http, _) = await client.PutAsync<int>("artifacts/settings", new { artifactRetentionDays = 10, maxArtifactBytes = 1024L * 1024 * 1024 });
        var audit = await _factory.DbAsync(db => db.StoreAuditLogs.AsNoTracking().Where(a => a.OrganizationId == org.OrganizationId && a.Action == AuditAction.IntakeSettingsUpdated).SingleAsync());

        http.StatusCode.Should().Be(HttpStatusCode.OK);
        audit.Channel.Should().Be(ActionChannel.Api);
        audit.UserId.Should().Be(org.Owner.Id);
    }
}
