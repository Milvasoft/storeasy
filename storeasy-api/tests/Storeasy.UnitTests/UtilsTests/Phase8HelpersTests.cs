using FluentAssertions;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using Storeasy.Application.Utils.PermissionManager;
using Storeasy.Domain.Enums;

namespace Storeasy.UnitTests.UtilsTests;

[Trait("Utils Unit Tests", "Phase 8 helpers: e-mail masking, platform permissions, cache keys, options and notification defaults.")]
public class Phase8HelpersTests
{
    [Theory]
    [InlineData("can@example.com", "c**@example.com")]
    [InlineData("a@example.com", "a**@example.com")]
    [InlineData("averyveryverylongname@example.com", "a********@example.com")]
    [InlineData("no-domain", "***")]
    [InlineData(null, null)]
    public void Mask_ShouldKeepOnlyTheFirstLetterAndTheDomain(string email, string expected)
    {
        EmailMasking.Mask(email).Should().Be(expected);
    }

    [Fact]
    public void Expand_ShouldGiveASuperAdminTheWholeCatalog()
    {
        var expanded = PlatformPermissions.Expand([PermissionCatalog.App.SuperAdmin]);
        var plain = PlatformPermissions.Expand([PermissionCatalog.RuleCatalogManagement.List, PermissionCatalog.RuleCatalogManagement.List]);

        expanded.Should().Contain([PermissionCatalog.App.SuperAdmin, PermissionCatalog.RuleCatalogManagement.Approve, PermissionCatalog.FeatureFlagManagement.Update, PermissionCatalog.UserManagement.Delete]);
        expanded.Should().BeInAscendingOrder(StringComparer.Ordinal);
        plain.Should().Equal(PermissionCatalog.RuleCatalogManagement.List);
        PlatformPermissions.Expand(null).Should().BeEmpty();
    }

    [Fact]
    public void CanGrant_ShouldReserveFullPrivilegeAndUnheldPermissions()
    {
        string[] full = [PermissionCatalog.App.SuperAdmin];
        string[] limited = [PermissionCatalog.RoleManagement.Create, PermissionCatalog.UserManagement.Update];

        PlatformPermissions.CanGrant(full, [PermissionCatalog.App.SuperAdmin, PermissionCatalog.FeatureFlagManagement.Update]).Should().BeTrue();
        PlatformPermissions.CanGrant(limited, [PermissionCatalog.UserManagement.Update]).Should().BeTrue();
        PlatformPermissions.CanGrant(limited, []).Should().BeTrue();
        PlatformPermissions.CanGrant(limited, [PermissionCatalog.FeatureFlagManagement.Update]).Should().BeFalse();
        PlatformPermissions.CanGrant(limited, [PermissionCatalog.App.SuperAdmin]).Should().BeFalse();
        PlatformPermissions.HasFullPrivilege(null).Should().BeFalse();
    }

    [Fact]
    public void Phase8Keys_ShouldUseTheApplicationPrefix()
    {
        var cacheKeys = new CacheKeys(new CacheKeyOptions { Prefix = "storeasy" });

        cacheKeys.GoogleSignInState("abc").Should().Be("storeasy:google-state:abc");
        cacheKeys.RevokedToken("f00").Should().Be("storeasy:revoked-token:f00");
    }

    [Fact]
    public void Options_ShouldBeOffUntilConfigured()
    {
        new GoogleAuthOptions().Enabled.Should().BeFalse();
        new GoogleAuthOptions { ClientId = "id" }.Enabled.Should().BeFalse();
        new GoogleAuthOptions { ClientId = "id", ClientSecret = "secret" }.Enabled.Should().BeTrue();
        new GoogleAuthOptions().Authority.Should().Be("https://accounts.google.com");
        new WebPushOptions().Enabled.Should().BeFalse();
        new WebPushOptions { PublicKey = "p", PrivateKey = "k" }.Enabled.Should().BeTrue();
        new EmailOptions().Outbox.Enabled.Should().BeTrue();
    }

    [Theory]
    [InlineData(NotificationType.Rejection, true)]
    [InlineData(NotificationType.ConnectionAlert, true)]
    [InlineData(NotificationType.NewReview, false)]
    [InlineData(NotificationType.JobSucceeded, false)]
    public void DefaultWebPush_ShouldFollowTheEmailDefault(NotificationType kind, bool expected)
    {
        NotificationKinds.DefaultWebPush(kind).Should().Be(expected);
    }
}
