using FluentAssertions;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Intake;
using Storeasy.Domain.Tokens;
using System.Net;

namespace Storeasy.UnitTests.IntakeTests;

[Trait("Intake Unit Tests", "Pure intake rules: files, parts, targets, exit codes, claim globs, hosts, addresses, webhook signatures, token prefixes.")]
public class IntakeRulesTests
{
    [Theory]
    [InlineData("halka-1.0.0-42.aab", ArtifactPlatform.Android, PackageKind.Aab)]
    [InlineData("app.APK", ArtifactPlatform.Android, PackageKind.Apk)]
    [InlineData("pusula.ipa", ArtifactPlatform.Ios, PackageKind.Ipa)]
    [InlineData("Pusula_2.4.0.0.msixbundle", ArtifactPlatform.Windows, PackageKind.Msix)]
    [InlineData("Pusula.appxupload", ArtifactPlatform.Windows, PackageKind.Msix)]
    public void Classify_ShouldKnowEverySupportedExtension(string fileName, ArtifactPlatform platform, PackageKind kind)
        => IntakeRules.Classify(fileName).Should().Be((platform, kind));

    [Theory]
    [InlineData("build.zip")]
    [InlineData("noextension")]
    [InlineData(null)]
    public void Classify_ShouldRefuseOtherFiles(string fileName) => IntakeRules.Classify(fileName).Should().BeNull();

    [Fact]
    public void PartLayout_ShouldFollowTheContract()
    {
        IntakeRules.PartSizeFor(50L * 1024 * 1024).Should().Be(8L * 1024 * 1024);
        IntakeRules.PartSizeFor(4L * 1024 * 1024 * 1024).Should().BeInRange(IntakeRules.MinPartSize, IntakeRules.MaxPartSize);
        IntakeRules.PartCount(4L * 1024 * 1024 * 1024, IntakeRules.PartSizeFor(4L * 1024 * 1024 * 1024)).Should().BeLessThanOrEqualTo(IntakeRules.MaxParts);
        IntakeRules.PartCount(50331648, 8388608).Should().Be(6);
        IntakeRules.PartCount(50331649, 8388608).Should().Be(7);
        IntakeRules.PartCount(0, 8388608).Should().Be(0);
    }

    [Theory]
    [InlineData(ArtifactPlatform.Android, DeliveryTarget.PlayTrack, "internal", true)]
    [InlineData(ArtifactPlatform.Android, DeliveryTarget.PlayTrack, "Production", false)]
    [InlineData(ArtifactPlatform.Android, DeliveryTarget.TestFlightGroup, "x", false)]
    [InlineData(ArtifactPlatform.Ios, DeliveryTarget.TestFlightGroup, "grp", true)]
    [InlineData(ArtifactPlatform.Ios, DeliveryTarget.TestFlightGroup, "", false)]
    [InlineData(ArtifactPlatform.Windows, DeliveryTarget.MsFlight, "flight-1", true)]
    [InlineData(ArtifactPlatform.Windows, DeliveryTarget.None, null, true)]
    public void Targets_ShouldBeTestChannelsOnly(ArtifactPlatform platform, DeliveryTarget target, string targetId, bool allowed)
        => IntakeRules.IsTargetAllowed(platform, target, targetId).Should().Be(allowed);

    [Fact]
    public void ExitCodes_ShouldMatchTheContract()
    {
        IntakeRules.ExitCodeOf(ArtifactSessionStatus.Delivered).Should().Be(0);
        IntakeRules.ExitCodeOf(ArtifactSessionStatus.Duplicate).Should().Be(0);
        IntakeRules.ExitCodeOf(ArtifactSessionStatus.Held).Should().Be(0);
        IntakeRules.ExitCodeOf(ArtifactSessionStatus.Rejected).Should().Be(2);
        IntakeRules.ExitCodeOf(ArtifactSessionStatus.Failed).Should().Be(4);
        IntakeRules.IsFinal(ArtifactSessionStatus.Delivering).Should().BeFalse();
    }

    [Theory]
    [InlineData("main", "main", true)]
    [InlineData("release/*", "release/1.2", true)]
    [InlineData("release/*", "feature/x", false)]
    [InlineData("*-hotfix", "a/b-hotfix", true)]
    [InlineData("main", "Main", false)]
    [InlineData("", "anything", true)]
    [InlineData("main", null, false)]
    public void Glob_ShouldMatchBranches(string pattern, string value, bool matches) => IntakeRules.GlobMatches(pattern, value).Should().Be(matches);

    [Theory]
    [InlineData("builds.expo.dev", true)]
    [InlineData("expo.dev", true)]
    [InlineData("evilexpo.dev", false)]
    [InlineData("expo.dev.evil.com", false)]
    [InlineData("API.CODEMAGIC.IO.", true)]
    public void Hosts_ShouldMatchExactOrSubdomain(string host, bool allowed)
        => IntakeRules.IsHostAllowed(host, ["expo.dev", "*.expo.dev", "api.codemagic.io"]).Should().Be(allowed);

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("10.1.2.3", true)]
    [InlineData("172.20.0.1", true)]
    [InlineData("192.168.1.20", true)]
    [InlineData("169.254.169.254", true)]
    [InlineData("100.64.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("fd00::1", true)]
    [InlineData("::ffff:10.0.0.1", true)]
    [InlineData("93.184.216.34", false)]
    [InlineData("2606:4700::1111", false)]
    public void InternalAddresses_ShouldBeBlocked(string address, bool blocked) => IntakeRules.IsBlockedAddress(IPAddress.Parse(address)).Should().Be(blocked);

    [Theory]
    [InlineData("expo.dev", true)]
    [InlineData("*.expo.dev", true)]
    [InlineData("https://expo.dev", false)]
    [InlineData("10.0.0.1", false)]
    [InlineData("localhost", false)]
    public void DomainEntries_ShouldBeHostNames(string entry, bool valid) => IntakeRules.IsValidDomainEntry(entry).Should().Be(valid);

    [Fact]
    public void WebhookSignatures_ShouldVerify()
    {
        var now = DateTime.UtcNow;
        var timestamp = new DateTimeOffset(now).ToUnixTimeSeconds().ToString();
        var old = new DateTimeOffset(now.AddMinutes(-6)).ToUnixTimeSeconds().ToString();

        PullWebhooks.IsValidEasSignature("s3cret", "{\"a\":1}", PullWebhooks.SignEas("s3cret", "{\"a\":1}")).Should().BeTrue();
        PullWebhooks.IsValidEasSignature("s3cret", "{\"a\":2}", PullWebhooks.SignEas("s3cret", "{\"a\":1}")).Should().BeFalse();
        PullWebhooks.IsValidEasSignature("s3cret", "x", "sha1=zz").Should().BeFalse();
        PullWebhooks.IsValidArtifactSignature("k", "{}", timestamp, PullWebhooks.SignArtifact("k", "{}", timestamp), now).Should().BeTrue();
        PullWebhooks.IsValidArtifactSignature("k", "{}", old, PullWebhooks.SignArtifact("k", "{}", old), now).Should().BeFalse();
        PullWebhooks.IsValidArtifactSignature("k", "{}", "abc", PullWebhooks.SignArtifact("k", "{}", "abc"), now).Should().BeFalse();
    }

    [Fact]
    public void TokenPrefixes_ShouldSelectTheKindAndBeRedacted()
    {
        TokenPrefixes.KindOf("sea_abc").Should().Be(TokenKind.PersonalAccessToken);
        TokenPrefixes.KindOf("sci_abc").Should().Be(TokenKind.CiToken);
        TokenPrefixes.KindOf("sut_abc").Should().Be(TokenKind.UploadToken);
        TokenPrefixes.KindOf("eyJhbGci").Should().BeNull();
        TokenPrefixes.DisplayPrefixOf("sea_9c1f4b7e").Should().Be("sea_9c1");
        SecretRedactor.Redact("token sea_9c1f4b7e2a6d0c83f15a9b2e7d4c61aa and sut_Q4n8Lr2VxT9mKp6Z").Should().NotContainAny("9c1f4b7e", "Q4n8Lr2V");
    }
}
