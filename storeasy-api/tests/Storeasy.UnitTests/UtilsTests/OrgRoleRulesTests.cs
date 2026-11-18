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
        OrgRoleRules.CanAssign(actor, current, next).Should().Be(expected);
    }

    [Theory]
    [InlineData(OrgRole.Owner, OrgRole.Owner, true)]
    [InlineData(OrgRole.Admin, OrgRole.Editor, true)]
    [InlineData(OrgRole.Admin, OrgRole.Owner, false)]
    [InlineData(OrgRole.Editor, OrgRole.Viewer, false)]
    public void CanRemove_ShouldProtectOwners(OrgRole actor, OrgRole member, bool expected)
    {
        OrgRoleRules.CanRemove(actor, member).Should().Be(expected);
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
