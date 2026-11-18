using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Application.Dtos.OrganizationDtos;
using Storeasy.Application.Dtos.StoreDtos;
using Storeasy.Domain.Enums;
using Storeasy.IntegrationTests.TestBase;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Organizations, members, invitations, store connections and the audit log, with role checks and isolation.")]
public class OrganizationsControllerTests(StoreasyApiFactory factory) : IntegrationTestBase(factory)
{
    private sealed record Member(TestUser User, string Email);

    private async Task<Member> NewMemberAsync()
    {
        var email = $"u{TestApi.Unique()}@example.com";
        var user = await _factory.CreateUserAsync(email: email);
        var login = await _factory.LoginAsync(user.UserName, TestApi.DefaultPassword);

        return new Member(login, email);
    }

    private static async Task<int> CreateOrganizationAsync(TestUser owner, string name = null)
    {
        var (http, body) = await owner.Client.PostAsync<int>("organizations/organization", new { name = name ?? $"Org {TestApi.Unique()}" });

        http.StatusCode.Should().Be(HttpStatusCode.OK, body?.Messages?.FirstOrDefault()?.Message);

        return body.Data;
    }

    /// <summary>Invites <paramref name="invitee"/> and accepts as them; returns the membership id.</summary>
    private async Task<int> AddMemberAsync(TestUser owner, int organizationId, Member invitee, OrgRole role)
    {
        var (_, invitation) = await owner.Client.PostAsync<InvitationCreatedDto>("organizations/invitation", new { organizationId, email = invitee.Email, role });
        var (acceptHttp, _) = await invitee.User.Client.PostAsync<int>("organizations/invitation/accept", new { token = invitation.Data.Token });

        acceptHttp.StatusCode.Should().Be(HttpStatusCode.OK);

        var (_, members) = await owner.Client.GetAsync<MemberListDto>($"organizations/members?organizationId={organizationId}");

        return members.Data.Members.Single(m => m.UserId == invitee.User.Id).Id;
    }

    private static async Task<(HttpResponseMessage Http, Response<T> Body)> DeleteWithBodyAsync<T>(HttpClient client, string path, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"{TestApi.Base}/{path}") { Content = JsonContent.Create(body, options: TestApi.Json) };

        var http = await client.SendAsync(request);

        return (http, await TestApi.ReadAsync<Response<T>>(http));
    }

    private static string AppleSecret()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        return JsonSerializer.Serialize(new { issuerId = "57246542-96fe-1a63-e053-0824d011072a", keyId = "ABC123DEFG", privateKey = key.ExportPkcs8PrivateKeyPem() });
    }

    [Fact]
    public async Task CreateOrganization_ShouldMakeTheCallerOwnerWithEveryPermission()
    {
        // Arrange
        var owner = await NewMemberAsync();

        // Act
        var organizationId = await CreateOrganizationAsync(owner.User);
        var (_, mine) = await owner.User.Client.GetAsync<List<MyOrganizationDto>>("organizations/mine");

        // Assert
        var organization = mine.Data.Should().ContainSingle(o => o.Id == organizationId).Subject;

        organization.Role.Should().Be(OrgRole.Owner);
        organization.Permissions.Should().Contain(OrgPermission.ManageOrganization);
    }

    [Fact]
    public async Task CreateOrganization_WithATakenSlug_ShouldBe409()
    {
        var owner = await NewMemberAsync();
        var name = $"Same {TestApi.Unique()}";

        await CreateOrganizationAsync(owner.User, name);

        var (http, _) = await owner.User.Client.PostAsync<int>("organizations/organization", new { name });

        http.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task NonMember_ShouldGet404ForEveryOrganizationEndpoint()
    {
        // Arrange
        var owner = await NewMemberAsync();
        var stranger = await NewMemberAsync();
        var organizationId = await CreateOrganizationAsync(owner.User);

        // Act
        var (members, _) = await stranger.User.Client.GetAsync<MemberListDto>($"organizations/members?organizationId={organizationId}");
        var (connections, _) = await stranger.User.Client.GetAsync<List<StoreConnectionDto>>($"storeconnections?organizationId={organizationId}");
        var (audit, _) = await stranger.User.Client.PatchListAsync<StoreAuditLogDto>("organizations/audit-logs", new { organizationId, pageNumber = 1, rowCount = 10 });
        var (add, _) = await stranger.User.Client.PostAsync<int>("storeconnections/connection", new { organizationId, store = StoreKind.Apple, name = "x", secret = AppleSecret() });

        // Assert: the same answer as for an organization that does not exist, so ids cannot be probed.
        members.StatusCode.Should().Be(HttpStatusCode.NotFound);
        connections.StatusCode.Should().Be(HttpStatusCode.NotFound);
        audit.StatusCode.Should().Be(HttpStatusCode.NotFound);
        add.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InviteAndAccept_ShouldAddTheMemberWithTheInvitedRole()
    {
        // Arrange
        var owner = await NewMemberAsync();
        var invitee = await NewMemberAsync();
        var organizationId = await CreateOrganizationAsync(owner.User);

        // Act
        await AddMemberAsync(owner.User, organizationId, invitee, OrgRole.Editor);

        var (_, mine) = await invitee.User.Client.GetAsync<List<MyOrganizationDto>>("organizations/mine");

        // Assert
        mine.Data.Should().ContainSingle(o => o.Id == organizationId).Which.Role.Should().Be(OrgRole.Editor);
    }

    [Fact]
    public async Task AcceptInvitation_ByAnotherUser_ShouldBe403AndTheTokenShouldStillWork()
    {
        // Arrange
        var owner = await NewMemberAsync();
        var invitee = await NewMemberAsync();
        var other = await NewMemberAsync();
        var organizationId = await CreateOrganizationAsync(owner.User);
        var (_, invitation) = await owner.User.Client.PostAsync<InvitationCreatedDto>("organizations/invitation", new { organizationId, email = invitee.Email, role = OrgRole.Viewer });

        // Act
        var (wrongUser, _) = await other.User.Client.PostAsync<int>("organizations/invitation/accept", new { token = invitation.Data.Token });
        var (rightUser, _) = await invitee.User.Client.PostAsync<int>("organizations/invitation/accept", new { token = invitation.Data.Token });
        var (reused, _) = await invitee.User.Client.PostAsync<int>("organizations/invitation/accept", new { token = invitation.Data.Token });

        // Assert
        wrongUser.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        rightUser.StatusCode.Should().Be(HttpStatusCode.OK);
        reused.StatusCode.Should().Be(HttpStatusCode.NotFound, "an invitation is single use");
    }

    [Fact]
    public async Task Viewer_ShouldNotManageConnectionsOrMembers()
    {
        // Arrange
        var owner = await NewMemberAsync();
        var viewer = await NewMemberAsync();
        var organizationId = await CreateOrganizationAsync(owner.User);

        await AddMemberAsync(owner.User, organizationId, viewer, OrgRole.Viewer);

        // Act
        var (add, _) = await viewer.User.Client.PostAsync<int>("storeconnections/connection", new { organizationId, store = StoreKind.Apple, name = "x", secret = AppleSecret() });
        var (invite, _) = await viewer.User.Client.PostAsync<InvitationCreatedDto>("organizations/invitation", new { organizationId, email = "x@example.com", role = OrgRole.Viewer });
        var (list, members) = await viewer.User.Client.GetAsync<MemberListDto>($"organizations/members?organizationId={organizationId}");

        // Assert
        add.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        invite.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        members.Data.Invitations.Should().BeEmpty("viewers do not see pending invitations");
    }

    [Fact]
    public async Task Admin_ShouldNotCreateOrTouchOwners()
    {
        // Arrange
        var owner = await NewMemberAsync();
        var admin = await NewMemberAsync();
        var editor = await NewMemberAsync();
        var organizationId = await CreateOrganizationAsync(owner.User);

        await AddMemberAsync(owner.User, organizationId, admin, OrgRole.Admin);
        var editorMemberId = await AddMemberAsync(owner.User, organizationId, editor, OrgRole.Editor);
        var (_, members) = await owner.User.Client.GetAsync<MemberListDto>($"organizations/members?organizationId={organizationId}");
        var ownerMemberId = members.Data.Members.Single(m => m.UserId == owner.User.Id).Id;

        // Act
        var (promote, _) = await admin.User.Client.PutAsync<int>("organizations/member/role", new { organizationId, memberId = editorMemberId, role = OrgRole.Owner });
        var (demoteOwner, _) = await admin.User.Client.PutAsync<int>("organizations/member/role", new { organizationId, memberId = ownerMemberId, role = OrgRole.Viewer });
        var (removeOwner, _) = await DeleteWithBodyAsync<int>(admin.User.Client, "organizations/member", new { organizationId, memberId = ownerMemberId });
        var (promoteToAdmin, _) = await admin.User.Client.PutAsync<int>("organizations/member/role", new { organizationId, memberId = editorMemberId, role = OrgRole.Admin });

        // Assert
        promote.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        demoteOwner.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        removeOwner.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        promoteToAdmin.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RoleChanges_ShouldNotEscalatePrivileges()
    {
        // Arrange
        var owner = await NewMemberAsync();
        var admin = await NewMemberAsync();
        var editor = await NewMemberAsync();
        var viewer = await NewMemberAsync();
        var organizationId = await CreateOrganizationAsync(owner.User);

        var adminMemberId = await AddMemberAsync(owner.User, organizationId, admin, OrgRole.Admin);
        await AddMemberAsync(owner.User, organizationId, editor, OrgRole.Editor);
        var viewerMemberId = await AddMemberAsync(owner.User, organizationId, viewer, OrgRole.Viewer);

        // Act
        var (adminSelfPromote, _) = await admin.User.Client.PutAsync<int>("organizations/member/role", new { organizationId, memberId = adminMemberId, role = OrgRole.Owner });
        var (adminInvitesOwner, _) = await admin.User.Client.PostAsync<InvitationCreatedDto>("organizations/invitation", new { organizationId, email = $"o{TestApi.Unique()}@example.com", role = OrgRole.Owner });
        var (editorPromotes, _) = await editor.User.Client.PutAsync<int>("organizations/member/role", new { organizationId, memberId = viewerMemberId, role = OrgRole.Admin });
        var (editorInvites, _) = await editor.User.Client.PostAsync<InvitationCreatedDto>("organizations/invitation", new { organizationId, email = $"e{TestApi.Unique()}@example.com", role = OrgRole.Viewer });
        var (_, mine) = await admin.User.Client.GetAsync<List<MyOrganizationDto>>("organizations/mine");

        // Assert
        adminSelfPromote.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        adminInvitesOwner.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Forbidden);
        editorPromotes.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        editorInvites.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        mine.Data.Single(o => o.Id == organizationId).Role.Should().Be(OrgRole.Admin);
    }

    [Fact]
    public async Task ConnectionList_ShouldShowFingerprintsOnlyToConnectionManagers()
    {
        // Arrange
        var owner = await NewMemberAsync();
        var viewer = await NewMemberAsync();
        var organizationId = await CreateOrganizationAsync(owner.User);

        await AddMemberAsync(owner.User, organizationId, viewer, OrgRole.Viewer);
        await owner.User.Client.PostAsync<int>("storeconnections/connection", new { organizationId, store = StoreKind.Apple, name = "Team key", secret = AppleSecret() });

        // Act
        var (_, ownerList) = await owner.User.Client.GetAsync<List<StoreConnectionDto>>($"storeconnections?organizationId={organizationId}");
        var (viewerHttp, viewerList) = await viewer.User.Client.GetAsync<List<StoreConnectionDto>>($"storeconnections?organizationId={organizationId}");

        // Assert
        ownerList.Data.Should().ContainSingle().Which.Fingerprint.KeyId.Should().Be("ABC123DEFG");
        viewerHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        var shown = viewerList.Data.Should().ContainSingle().Subject;
        shown.Name.Should().Be("Team key");
        shown.Fingerprint.Should().BeNull();
        shown.LastVerifyError.Should().BeNull();
    }

    [Fact]
    public async Task UpdateOrganization_ShouldChangeTheSlugUniquelyAndOnlyForOwners()
    {
        // Arrange
        var owner = await NewMemberAsync();
        var admin = await NewMemberAsync();
        var organizationId = await CreateOrganizationAsync(owner.User);
        var otherName = $"Taken {TestApi.Unique()}";
        await CreateOrganizationAsync(owner.User, otherName);
        await AddMemberAsync(owner.User, organizationId, admin, OrgRole.Admin);
        var newSlug = $"New Slug {TestApi.Unique()}";

        // Act
        var (changed, _) = await owner.User.Client.PutAsync<int>("organizations/organization", new { organizationId, name = "Renamed", slug = newSlug });
        var (taken, _) = await owner.User.Client.PutAsync<int>("organizations/organization", new { organizationId, name = "Renamed", slug = otherName });
        var (invalid, _) = await owner.User.Client.PutAsync<int>("organizations/organization", new { organizationId, name = "Renamed", slug = "a" });
        var (byAdmin, _) = await admin.User.Client.PutAsync<int>("organizations/organization", new { organizationId, name = "Admin rename", slug = $"adm-{TestApi.Unique()}" });
        var row = await _factory.DbAsync(db => db.Organizations.AsNoTracking().SingleAsync(o => o.Id == organizationId));

        // Assert
        changed.StatusCode.Should().Be(HttpStatusCode.OK);
        taken.StatusCode.Should().Be(HttpStatusCode.Conflict);
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        byAdmin.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        row.Name.Should().Be("Renamed");
        row.Slug.Should().Be(Storeasy.Domain.Organizations.OrgRoleRules.ToSlug(newSlug));
    }

    [Fact]
    public async Task LastOwner_ShouldNotLeaveOrBeDemoted()
    {
        // Arrange
        var owner = await NewMemberAsync();
        var organizationId = await CreateOrganizationAsync(owner.User);
        var (_, members) = await owner.User.Client.GetAsync<MemberListDto>($"organizations/members?organizationId={organizationId}");
        var ownerMemberId = members.Data.Members.Single().Id;

        // Act
        var (leave, _) = await DeleteWithBodyAsync<int>(owner.User.Client, "organizations/member", new { organizationId, memberId = ownerMemberId });
        var (demote, _) = await owner.User.Client.PutAsync<int>("organizations/member/role", new { organizationId, memberId = ownerMemberId, role = OrgRole.Admin });

        // Assert
        leave.StatusCode.Should().Be(HttpStatusCode.Conflict);
        demote.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RemovedMember_ShouldLoseAccessImmediately()
    {
        // Arrange
        var owner = await NewMemberAsync();
        var editor = await NewMemberAsync();
        var organizationId = await CreateOrganizationAsync(owner.User);
        var editorMemberId = await AddMemberAsync(owner.User, organizationId, editor, OrgRole.Editor);

        // The editor's role is now cached.
        var (before, _) = await editor.User.Client.GetAsync<List<StoreConnectionDto>>($"storeconnections?organizationId={organizationId}");

        // Act
        await DeleteWithBodyAsync<int>(owner.User.Client, "organizations/member", new { organizationId, memberId = editorMemberId });

        var (after, _) = await editor.User.Client.GetAsync<List<StoreConnectionDto>>($"storeconnections?organizationId={organizationId}");

        // Assert
        before.StatusCode.Should().Be(HttpStatusCode.OK);
        after.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddConnection_ShouldStoreTheSecretEncryptedAndNeverReturnIt()
    {
        // Arrange
        var owner = await NewMemberAsync();
        var organizationId = await CreateOrganizationAsync(owner.User);
        var secret = AppleSecret();

        // Act
        var (addHttp, added) = await owner.User.Client.PostAsync<int>("storeconnections/connection", new { organizationId, store = StoreKind.Apple, name = "Team key", secret });
        var listHttp = await owner.User.Client.GetAsync($"{TestApi.Base}/storeconnections?organizationId={organizationId}");
        var listBody = await listHttp.Content.ReadAsStringAsync();
        var (verifyHttp, verified) = await owner.User.Client.PostAsync<StoreConnectionDto>("storeconnections/connection/verify", new { organizationId, connectionId = added.Data });

        var row = await _factory.DbAsync(db => db.StoreConnections.AsNoTracking().SingleAsync(c => c.Id == added.Data));

        // Assert: the response carries only the fingerprint.
        addHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        listBody.Should().Contain("ABC123DEFG").And.Contain("072a").And.NotContain("PRIVATE KEY").And.NotContain("57246542");

        // Assert: nothing readable at rest.
        Encoding.UTF8.GetString(row.EncryptedSecret).Should().NotContain("PRIVATE KEY").And.NotContain("ABC123DEFG");
        row.EncryptedDataKey.Should().NotBeEmpty();

        // Assert: the sealed secret opens to the normalized original for the store call.
        verifyHttp.StatusCode.Should().Be(HttpStatusCode.OK);
        verified.Data.Status.Should().Be(ConnectionStatus.Verified);
        JsonDocument.Parse(_factory.StoreGateway.VerifiedSecrets.Last()).RootElement.GetProperty("privateKey").GetString()
                    .Should().Be(JsonDocument.Parse(secret).RootElement.GetProperty("privateKey").GetString());
    }

    [Fact]
    public async Task AddConnection_WithMalformedSecret_ShouldBe400()
    {
        var owner = await NewMemberAsync();
        var organizationId = await CreateOrganizationAsync(owner.User);

        var (http, _) = await owner.User.Client.PostAsync<int>("storeconnections/connection", new { organizationId, store = StoreKind.Google, name = "Play", secret = "{\"type\":\"user\"}" });

        http.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddConnection_ForMicrosoftStore_WithMalformedSecret_ShouldBe400()
    {
        var owner = await NewMemberAsync();
        var organizationId = await CreateOrganizationAsync(owner.User);

        var (http, body) = await owner.User.Client.PostAsync<int>("storeconnections/connection", new { organizationId, store = StoreKind.Microsoft, name = "Partner Center", secret = "{\"tenantId\":\"x\"}", credentialExpiryDate = DateTime.UtcNow.AddMonths(6) });

        http.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Messages.Should().Contain(m => m.Key == Storeasy.Application.Utils.Constants.MessageKey.MsSecretInvalid);
    }

    [Fact]
    public async Task DeleteConnection_ShouldWipeTheSecret()
    {
        // Arrange
        var owner = await NewMemberAsync();
        var organizationId = await CreateOrganizationAsync(owner.User);
        var (_, added) = await owner.User.Client.PostAsync<int>("storeconnections/connection", new { organizationId, store = StoreKind.Apple, name = "Old key", secret = AppleSecret() });

        // Act
        var (http, _) = await DeleteWithBodyAsync<int>(owner.User.Client, "storeconnections/connection", new { organizationId, connectionId = added.Data });

        var row = await _factory.DbAsync(db => db.StoreConnections.IgnoreQueryFilters().AsNoTracking().SingleAsync(c => c.Id == added.Data));

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.OK);
        row.EncryptedSecret.Should().BeEmpty();
        row.EncryptedDataKey.Should().BeEmpty();
    }

    [Fact]
    public async Task AuditLog_ShouldListTheWritesNewestFirstWithoutSecrets()
    {
        // Arrange
        var owner = await NewMemberAsync();
        var organizationId = await CreateOrganizationAsync(owner.User);

        await owner.User.Client.PostAsync<int>("storeconnections/connection", new { organizationId, store = StoreKind.Apple, name = "Audited key", secret = AppleSecret() });

        // Act
        var (http, logs) = await owner.User.Client.PatchListAsync<StoreAuditLogDto>("organizations/audit-logs", new { organizationId, pageNumber = 1, rowCount = 10 });

        // Assert
        http.StatusCode.Should().Be(HttpStatusCode.OK);
        logs.Data.Select(l => l.Action).Should().Equal(AuditAction.ConnectionAdded, AuditAction.OrganizationCreated);
        logs.Data[0].Channel.Should().Be(ActionChannel.Web);
        logs.Data[0].Details.Should().Contain("ABC123DEFG").And.NotContain("PRIVATE");
    }
}
