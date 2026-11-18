using FluentAssertions;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Organizations;

namespace Storeasy.UnitTests.UtilsTests;

[Trait("Utils Unit Tests", "Organization role rules unit tests.")]
public class OrgRoleRulesTests
{
    [Theory]
    [InlineData(OrgRole.Owner, OrgPermission.ManageOrganization, true)]
    [InlineData(OrgRole.Admin, OrgPermission.ManageOrganization, false)]
    [InlineData(OrgRole.Admin, OrgPermission.SubmitForReview, true)]
    [InlineData(OrgRole.Admin, OrgPermission.ManageConnections, true)]
    [InlineData(OrgRole.Editor, OrgPermission.EditContent, true)]
    [InlineData(OrgRole.Editor, OrgPermission.ReleaseTest, true)]
    [InlineData(OrgRole.Editor, OrgPermission.ReleaseProduction, false)]
    [InlineData(OrgRole.Editor, OrgPermission.SubmitForReview, false)]
    [InlineData(OrgRole.Editor, OrgPermission.ManageConnections, false)]
    [InlineData(OrgRole.Viewer, OrgPermission.View, true)]
    [InlineData(OrgRole.Viewer, OrgPermission.EditContent, false)]
    [InlineData(OrgRole.Viewer, OrgPermission.ReplyReviews, false)]
    public void Allows_ShouldFollowTheRoleMatrix(OrgRole role, OrgPermission permission, bool expected)
    {
        OrgRoleRules.Allows(role, permission).Should().Be(expected);
    }

    [Fact]
    public void Owner_ShouldHaveEveryPermission()
    {
        OrgRoleRules.PermissionsOf(OrgRole.Owner).Should().BeEquivalentTo(Enum.GetValues<OrgPermission>());
    }

    [Fact]
    public void EveryRole_ShouldBeAllowedToView()
    {
        foreach (var role in Enum.GetValues<OrgRole>())
            OrgRoleRules.Allows(role, OrgPermission.View).Should().BeTrue();
    }

    [Theory]
    [InlineData(OrgRole.Owner, OrgRole.Owner, OrgRole.Admin, true)]
    [InlineData(OrgRole.Owner, OrgRole.Viewer, OrgRole.Owner, true)]
    [InlineData(OrgRole.Admin, OrgRole.Viewer, OrgRole.Editor, true)]
    [InlineData(OrgRole.Admin, OrgRole.Owner, OrgRole.Admin, false)]
    [InlineData(OrgRole.Admin, OrgRole.Editor, OrgRole.Owner, false)]
    [InlineData(OrgRole.Admin, OrgRole.Admin, OrgRole.Owner, false)]
    [InlineData(OrgRole.Editor, OrgRole.Viewer, OrgRole.Admin, false)]
    [InlineData(OrgRole.Viewer, OrgRole.Viewer, OrgRole.Owner, false)]
    [InlineData(OrgRole.Editor, OrgRole.Viewer, OrgRole.Editor, false)]
    [InlineData(OrgRole.Viewer, OrgRole.Viewer, OrgRole.Viewer, false)]
    public void CanAssign_ShouldProtectOwners(OrgRole actor, OrgRole current, OrgRole next, bool expected)
    {
        OrgRoleRules.CanAssign(OrgAccess.Seeded(actor), OrgAccess.Seeded(current), OrgAccess.Seeded(next)).Should().Be(expected);
    }

    [Theory]
    [InlineData(OrgRole.Owner, OrgRole.Owner, true)]
    [InlineData(OrgRole.Admin, OrgRole.Editor, true)]
    [InlineData(OrgRole.Admin, OrgRole.Owner, false)]
    [InlineData(OrgRole.Editor, OrgRole.Viewer, false)]
    public void CanRemove_ShouldProtectOwners(OrgRole actor, OrgRole member, bool expected)
    {
        OrgRoleRules.CanRemove(OrgAccess.Seeded(actor), OrgAccess.Seeded(member)).Should().Be(expected);
    }

    [Fact]
    public void NormalizeCustom_ShouldAlwaysViewAndNeverOwnerOnly()
    {
        OrgRoleRules.NormalizeCustom([OrgPermission.ManageOrganization, OrgPermission.EditContent, OrgPermission.EditContent, (OrgPermission)200])
                    .Should().Equal(OrgPermission.View, OrgPermission.EditContent);
        OrgRoleRules.NormalizeCustom(null).Should().Equal(OrgPermission.View);
    }

    [Fact]
    public void CustomAccess_ShouldGrantOnlyItsOwnSet()
    {
        var access = OrgAccess.Custom(7, [OrgPermission.ReplyReviews]);

        access.Role.Should().Be(OrgRole.Custom);
        access.CustomRoleId.Should().Be(7);
        access.Allows(OrgPermission.ReplyReviews).Should().BeTrue();
        access.Allows(OrgPermission.View).Should().BeTrue();
        access.Allows(OrgPermission.EditContent).Should().BeFalse();
        access.CanWrite.Should().BeTrue();
        access.AsReadOnly().CanWrite.Should().BeFalse();
        OrgAccess.Of(OrgRole.Custom, null, null).Permissions.Should().BeEquivalentTo([OrgPermission.View]);
    }

    [Fact]
    public void CanAssign_ShouldNotGiveMoreThanTheActorHolds()
    {
        var manager = OrgAccess.Custom(1, [OrgPermission.ManageMembers, OrgPermission.EditContent]);
        var editorLike = OrgAccess.Custom(2, [OrgPermission.EditContent]);
        var releaser = OrgAccess.Custom(3, [OrgPermission.ReleaseProduction]);

        OrgRoleRules.CanAssign(manager, OrgAccess.Seeded(OrgRole.Viewer), editorLike).Should().BeTrue();
        OrgRoleRules.CanAssign(manager, null, releaser).Should().BeFalse("the manager cannot release to production");
        OrgRoleRules.CanAssign(manager, OrgAccess.Seeded(OrgRole.Admin), editorLike).Should().BeFalse("an admin can do more than the manager");
        OrgRoleRules.CanAssign(editorLike, null, OrgAccess.Seeded(OrgRole.Viewer)).Should().BeFalse("without ManageMembers");
        OrgRoleRules.CanAssign(OrgAccess.Seeded(OrgRole.Admin), null, releaser).Should().BeTrue();
        OrgRoleRules.CanRemove(manager, OrgAccess.Seeded(OrgRole.Editor)).Should().BeFalse("an editor also releases to test tracks");
        OrgRoleRules.CanRemove(manager, editorLike).Should().BeTrue();
    }

    [Fact]
    public void CanDefine_ShouldNeedEveryPermissionInvolved()
    {
        var admin = OrgAccess.Seeded(OrgRole.Admin);

        OrgRoleRules.CanDefine(admin, [OrgPermission.SubmitForReview]).Should().BeTrue();
        OrgRoleRules.CanDefine(OrgAccess.Seeded(OrgRole.Editor), [OrgPermission.View]).Should().BeFalse();
        OrgRoleRules.CanDefine(OrgAccess.Custom(1, [OrgPermission.ManageMembers]), [OrgPermission.View], [OrgPermission.ManageWebhooks]).Should().BeFalse();
    }

    [Theory]
    [InlineData(new[] { OrgPermission.View }, OrgRole.Viewer)]
    [InlineData(new[] { OrgPermission.EditContent, OrgPermission.ReleaseTest, OrgPermission.ReplyReviews }, OrgRole.Editor)]
    [InlineData(new[] { OrgPermission.EditContent, OrgPermission.ReleaseTest, OrgPermission.ReplyReviews, OrgPermission.ReleaseProduction, OrgPermission.SubmitForReview, OrgPermission.ManageConnections, OrgPermission.ManageWebhooks, OrgPermission.ManageMembers }, OrgRole.Admin)]
    public void AudienceRoleOf_ShouldMatchTheLargestSeededSubset(OrgPermission[] permissions, OrgRole expected)
    {
        OrgRoleRules.AudienceRoleOf(OrgRoleRules.NormalizeCustom(permissions)).Should().Be(expected);
    }

    [Fact]
    public void RemovesLastOwner_ShouldOnlyBlockTheLastOwner()
    {
        OrgRoleRules.RemovesLastOwner(1, OrgRole.Owner, OrgRole.Admin).Should().BeTrue();
        OrgRoleRules.RemovesLastOwner(1, OrgRole.Owner, null).Should().BeTrue();
        OrgRoleRules.RemovesLastOwner(2, OrgRole.Owner, null).Should().BeFalse();
        OrgRoleRules.RemovesLastOwner(1, OrgRole.Admin, null).Should().BeFalse();
        OrgRoleRules.RemovesLastOwner(1, OrgRole.Owner, OrgRole.Owner).Should().BeFalse();
    }

    [Theory]
    [InlineData("Milvasoft", "milvasoft")]
    [InlineData("  Çağrı Şirketi Ödül  ", "cagri-sirketi-odul")]
    [InlineData("a--b__c", "a-b-c")]
    [InlineData("!!!", "")]
    [InlineData(null, "")]
    public void ToSlug_ShouldNormalize(string input, string expected)
    {
        OrgRoleRules.ToSlug(input).Should().Be(expected);
    }

    [Fact]
    public void ToSlug_ShouldTrimToTheColumnLength()
    {
        OrgRoleRules.ToSlug(new string('a', 100)).Length.Should().Be(Organization.SlugMaxLength);
    }
}
