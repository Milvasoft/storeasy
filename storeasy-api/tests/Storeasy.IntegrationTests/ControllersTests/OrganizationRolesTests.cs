using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Application.Dtos.OrganizationDtos;
using Storeasy.Application.Utils.Constants;
using Storeasy.Domain.Enums;
using Storeasy.IntegrationTests.TestBase;
using System.Net;
using System.Net.Http.Json;

namespace Storeasy.IntegrationTests.ControllersTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Controller Integration Tests", "Custom organization roles: definition, assignment limits, access checks, invitations, tokens and isolation.")]
public class OrganizationRolesTests(StoreasyApiFactory factory) : IntegrationTestBase(factory)
{
    private async Task<(TestUser Owner, int OrganizationId)> NewOrganizationAsync()
    {
        var owner = await _factory.CreateAndLoginUserAsync();
        var (_, body) = await owner.Client.PostAsync<int>("organizations/organization", new { name = $"Org {TestApi.Unique()}" });

        return (owner, body.Data);
    }

    private static Task<(HttpResponseMessage Http, Response<int> Body)> CreateRoleAsync(TestUser actor, int organizationId, string name, params OrgPermission[] permissions)
        => actor.Client.PostAsync<int>("organizations/role", new { organizationId, name, description = "test role", permissions });

    private static async Task<(HttpResponseMessage Http, Response<T> Body)> DeleteWithBodyAsync<T>(HttpClient client, string path, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"{TestApi.Base}/{path}") { Content = JsonContent.Create(body, options: TestApi.Json) };

        var http = await client.SendAsync(request);

        return (http, await TestApi.ReadAsync<Response<T>>(http));
    }

    private async Task<int> MemberIdAsync(int organizationId, int userId)
        => await _factory.DbAsync(db => db.OrganizationMembers.Where(m => m.OrganizationId == organizationId && m.UserId == userId).Select(m => m.Id).SingleAsync());

    private static Task<(HttpResponseMessage Http, Response<int> Body)> EditProfileAsync(TestUser user, int organizationId)
        => user.Client.PutAsync<int>("apps/profile", new { organizationId, appId = int.MaxValue });

    [Fact]
    public async Task CustomRole_ShouldGrantExactlyItsPermissions()
    {
        // Arrange
        var (owner, organizationId) = await NewOrganizationAsync();
        var member = await _factory.AddMemberAsync(organizationId, OrgRole.Viewer);
        var (_, role) = await CreateRoleAsync(owner, organizationId, "Reviewer", OrgPermission.ReplyReviews, OrgPermission.EditContent);
        var memberId = await MemberIdAsync(organizationId, member.Id);

        var (asViewer, _) = await EditProfileAsync(member, organizationId);

        // Act
        var (assign, _) = await owner.Client.PutAsync<int>("organizations/member/role", new { organizationId, memberId, role = OrgRole.Custom, customRoleId = role.Data });
        var (asCustom, asCustomBody) = await EditProfileAsync(member, organizationId);
        var (connections, _) = await member.Client.PostAsync<int>("storeconnections/connection", new { organizationId, store = StoreKind.Apple, name = "x", secret = "{}" });
        var (_, mine) = await member.Client.GetAsync<List<MyOrganizationDto>>("organizations/mine");
        var (_, roles) = await owner.Client.GetAsync<List<OrgRoleDto>>($"organizations/roles?organizationId={organizationId}");

        // Assert
        asViewer.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        assign.StatusCode.Should().Be(HttpStatusCode.OK);
        asCustom.StatusCode.Should().Be(HttpStatusCode.NotFound, "the role edits content, so the request reaches the handler ({0})", asCustomBody?.Messages?.FirstOrDefault()?.Key);
        connections.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var organization = mine.Data.Single(o => o.Id == organizationId);

        organization.Role.Should().Be(OrgRole.Custom);
        organization.CustomRoleId.Should().Be(role.Data);
        organization.CustomRoleName.Should().Be("Reviewer");
        organization.Permissions.Should().Equal(OrgPermission.View, OrgPermission.EditContent, OrgPermission.ReplyReviews);

        roles.Data.Where(r => r.IsLocked).Select(r => r.Role).Should().Equal(OrgRole.Owner, OrgRole.Admin, OrgRole.Editor, OrgRole.Viewer);
        roles.Data.Should().ContainSingle(r => r.Id == role.Data && !r.IsLocked && r.MemberCount == 1 && r.Name == "Reviewer");
    }

    [Fact]
    public async Task UpdatingTheRole_ShouldChangeItsMembersAtOnce()
    {
        // Arrange
        var (owner, organizationId) = await NewOrganizationAsync();
        var member = await _factory.AddMemberAsync(organizationId, OrgRole.Viewer);
        var (_, role) = await CreateRoleAsync(owner, organizationId, "Writers", OrgPermission.EditContent);
        await owner.Client.PutAsync<int>("organizations/member/role", new { organizationId, memberId = await MemberIdAsync(organizationId, member.Id), role = OrgRole.Custom, customRoleId = role.Data });

        // The member's access is cached now.
        var (before, _) = await EditProfileAsync(member, organizationId);

        // Act
        var (update, _) = await owner.Client.PutAsync<int>("organizations/role", new { organizationId, roleId = role.Data, name = "Readers", permissions = new[] { OrgPermission.View } });
        var (after, _) = await EditProfileAsync(member, organizationId);
        var audit = await _factory.DbAsync(db => db.StoreAuditLogs.Where(a => a.OrganizationId == organizationId).Select(a => a.Action).ToListAsync());

        // Assert
        before.StatusCode.Should().Be(HttpStatusCode.NotFound);
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        after.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        audit.Should().Contain([AuditAction.OrganizationRoleCreated, AuditAction.OrganizationRoleUpdated, AuditAction.MemberRoleChanged]);
    }

    [Fact]
    public async Task Roles_ShouldNotExceedTheActorsOwnPermissions()
    {
        // Arrange
        var (owner, organizationId) = await NewOrganizationAsync();
        var manager = await _factory.AddMemberAsync(organizationId, OrgRole.Viewer);
        var admin = await _factory.AddMemberAsync(organizationId, OrgRole.Admin);
        var editor = await _factory.AddMemberAsync(organizationId, OrgRole.Editor);
        var (_, managerRole) = await CreateRoleAsync(owner, organizationId, "Member managers", OrgPermission.ManageMembers, OrgPermission.EditContent);
        await owner.Client.PutAsync<int>("organizations/member/role", new { organizationId, memberId = await MemberIdAsync(organizationId, manager.Id), role = OrgRole.Custom, customRoleId = managerRole.Data });
        var (_, releaserRole) = await CreateRoleAsync(owner, organizationId, "Releasers", OrgPermission.ReleaseProduction);

        // Act
        var (ownerOnly, ownerOnlyBody) = await CreateRoleAsync(owner, organizationId, "Org admins", OrgPermission.ManageOrganization);
        var (beyondOwn, _) = await CreateRoleAsync(manager, organizationId, "Releasers 2", OrgPermission.ReleaseProduction);
        var (withinOwn, _) = await CreateRoleAsync(manager, organizationId, "Editors lite", OrgPermission.EditContent);
        var (editorCreates, _) = await CreateRoleAsync(editor, organizationId, "Nope", OrgPermission.View);
        var (assignBeyond, _) = await manager.Client.PutAsync<int>("organizations/member/role", new { organizationId, memberId = await MemberIdAsync(organizationId, editor.Id), role = OrgRole.Custom, customRoleId = releaserRole.Data });
        var (demoteAdmin, _) = await manager.Client.PutAsync<int>("organizations/member/role", new { organizationId, memberId = await MemberIdAsync(organizationId, admin.Id), role = OrgRole.Viewer });
        var (inviteBeyond, _) = await manager.Client.PostAsync<InvitationCreatedDto>("organizations/invitation", new { organizationId, email = $"i{TestApi.Unique()}@example.com", role = OrgRole.Custom, customRoleId = releaserRole.Data });
        var (adminAssigns, _) = await admin.Client.PutAsync<int>("organizations/member/role", new { organizationId, memberId = await MemberIdAsync(organizationId, editor.Id), role = OrgRole.Custom, customRoleId = releaserRole.Data });

        // Assert
        ownerOnly.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ownerOnlyBody.Messages.Should().Contain(m => m.Message != null);
        beyondOwn.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        withinOwn.StatusCode.Should().Be(HttpStatusCode.OK);
        editorCreates.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        assignBeyond.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        demoteAdmin.StatusCode.Should().Be(HttpStatusCode.Forbidden, "an admin can do more than the manager");
        inviteBeyond.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        adminAssigns.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task InvitationWithACustomRole_ShouldJoinWithIt_AndTheRoleShouldNotBeDeletedWhileHeld()
    {
        // Arrange
        var (owner, organizationId) = await NewOrganizationAsync();
        var (_, role) = await CreateRoleAsync(owner, organizationId, "Testers", OrgPermission.ReleaseTest);
        var email = $"i{TestApi.Unique()}@example.com";
        var invitee = await _factory.CreateUserAsync(email: email);
        var login = await _factory.LoginAsync(invitee.UserName, TestApi.DefaultPassword);

        // Act
        var (_, invitation) = await owner.Client.PostAsync<InvitationCreatedDto>("organizations/invitation", new { organizationId, email, role = OrgRole.Custom, customRoleId = role.Data });
        var (_, preview) = await _factory.CreateClientWithAddress().GetAsync<InvitationPreviewDto>($"organizations/invitation/preview?token={Uri.EscapeDataString(invitation.Data.Token)}");
        var (deleteWhileInvited, _) = await DeleteWithBodyAsync<int>(owner.Client, "organizations/role", new { organizationId, roleId = role.Data });
        await login.Client.PostAsync<int>("organizations/invitation/accept", new { token = invitation.Data.Token });
        var membership = await _factory.DbAsync(db => db.OrganizationMembers.AsNoTracking().SingleAsync(m => m.OrganizationId == organizationId && m.UserId == invitee.Id));
        var (deleteWhileHeld, deleteBody) = await DeleteWithBodyAsync<int>(owner.Client, "organizations/role", new { organizationId, roleId = role.Data });
        await owner.Client.PutAsync<int>("organizations/member/role", new { organizationId, memberId = membership.Id, role = OrgRole.Viewer });
        var (deleteFree, _) = await DeleteWithBodyAsync<int>(owner.Client, "organizations/role", new { organizationId, roleId = role.Data });

        // Assert
        preview.Data.Status.Should().Be(InvitationStatus.Pending);
        preview.Data.Role.Should().Be(OrgRole.Custom);
        preview.Data.CustomRoleName.Should().Be("Testers");
        preview.Data.Permissions.Should().Equal(OrgPermission.View, OrgPermission.ReleaseTest);
        deleteWhileInvited.StatusCode.Should().Be(HttpStatusCode.Conflict);
        membership.Role.Should().Be(OrgRole.Custom);
        membership.CustomRoleId.Should().Be(role.Data);
        deleteWhileHeld.StatusCode.Should().Be(HttpStatusCode.Conflict);
        deleteBody.Messages.Should().Contain(m => m.Key == MessageKey.OrganizationRoleInUse);
        deleteFree.StatusCode.Should().Be(HttpStatusCode.OK);
        (await _factory.DbAsync(db => db.OrganizationRoles.AnyAsync(r => r.Id == role.Data))).Should().BeFalse();
    }

    [Fact]
    public async Task AnotherOrganizationsRole_ShouldBe404()
    {
        // Arrange
        var (owner, organizationId) = await NewOrganizationAsync();
        var (otherOwner, otherOrganizationId) = await NewOrganizationAsync();
        var member = await _factory.AddMemberAsync(organizationId, OrgRole.Viewer);
        var (_, foreignRole) = await CreateRoleAsync(otherOwner, otherOrganizationId, "Foreign", OrgPermission.EditContent);

        // Act
        var (assign, _) = await owner.Client.PutAsync<int>("organizations/member/role", new { organizationId, memberId = await MemberIdAsync(organizationId, member.Id), role = OrgRole.Custom, customRoleId = foreignRole.Data });
        var (update, _) = await owner.Client.PutAsync<int>("organizations/role", new { organizationId, roleId = foreignRole.Data, name = "Mine", permissions = new[] { OrgPermission.View } });
        var (delete, _) = await DeleteWithBodyAsync<int>(owner.Client, "organizations/role", new { organizationId, roleId = foreignRole.Data });
        var (list, _) = await owner.Client.GetAsync<List<OrgRoleDto>>($"organizations/roles?organizationId={otherOrganizationId}");
        var (duplicate, _) = await CreateRoleAsync(otherOwner, otherOrganizationId, "FOREIGN", OrgPermission.View);

        // Assert
        assign.StatusCode.Should().Be(HttpStatusCode.NotFound);
        update.StatusCode.Should().Be(HttpStatusCode.NotFound);
        delete.StatusCode.Should().Be(HttpStatusCode.NotFound);
        list.StatusCode.Should().Be(HttpStatusCode.NotFound);
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PersonalAccessToken_ShouldFollowTheCustomRole()
    {
        // Arrange
        var (owner, organizationId) = await NewOrganizationAsync();
        var member = await _factory.AddMemberAsync(organizationId, OrgRole.Viewer);
        var (_, role) = await CreateRoleAsync(owner, organizationId, "Writers", OrgPermission.EditContent);
        await owner.Client.PutAsync<int>("organizations/member/role", new { organizationId, memberId = await MemberIdAsync(organizationId, member.Id), role = OrgRole.Custom, customRoleId = role.Data });

        // Act
        var (writing, writingBody) = await member.Client.PostAsync<Application.Dtos.TokenDtos.PersonalAccessTokenCreatedDto>("tokens/token", new { organizationId, name = $"w{TestApi.Unique()}", readOnly = false, expiryDays = 30 });
        var (readOnly, readOnlyBody) = await member.Client.PostAsync<Application.Dtos.TokenDtos.PersonalAccessTokenCreatedDto>("tokens/token", new { organizationId, name = $"r{TestApi.Unique()}", readOnly = true, expiryDays = 30 });
        var writingClient = _factory.CreateClientWithAddress().UseAccessToken(writingBody.Data.Token);
        var readOnlyClient = _factory.CreateClientWithAddress().UseAccessToken(readOnlyBody.Data.Token);
        var (writeWithWriting, _) = await EditProfileAsync(new TestUser { Client = writingClient }, organizationId);
        var (writeWithReadOnly, _) = await EditProfileAsync(new TestUser { Client = readOnlyClient }, organizationId);

        // Assert
        writing.StatusCode.Should().Be(HttpStatusCode.OK);
        readOnly.StatusCode.Should().Be(HttpStatusCode.OK);
        writeWithWriting.StatusCode.Should().Be(HttpStatusCode.NotFound, "the custom role edits content");
        writeWithReadOnly.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
