using FluentAssertions;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Intake;

namespace Storeasy.UnitTests.UtilsTests;

[Trait("Utils Unit Tests", "Matching a synced build to the package storeasy keeps.")]
public class BuildPackagesTests
{
    private static ArtifactSession Session(string versionName, string versionCode, string storeBuildId = null)
        => new() { StoreBuildId = storeBuildId, Package = new ArtifactPackageInfo { VersionName = versionName, VersionCode = versionCode } };

    [Fact]
    public void StoreBuildId_ShouldMatchTheExternalId()
        => BuildPackages.Matches(StoreKind.Apple, "asc-build-1", "2.0", "7", Session("1.0", "1", "asc-build-1")).Should().BeTrue();

    [Theory]
    [InlineData("1.2.0", "15", true)]
    [InlineData("1.2.0", "16", false)]
    [InlineData("1.3.0", "15", false)]
    public void Apple_ShouldNeedBothVersionAndBuildNumber(string version, string buildNumber, bool expected)
        => BuildPackages.Matches(StoreKind.Apple, "x", version, buildNumber, Session("1.2.0", "15")).Should().Be(expected);

    [Theory]
    [InlineData(null, "42", true)]
    [InlineData("1.0.0", "42", true)]
    [InlineData("1.0.1", "42", false)]
    [InlineData(null, "43", false)]
    public void Google_ShouldMatchTheVersionCodeAndTheNameWhenKnown(string version, string buildNumber, bool expected)
        => BuildPackages.Matches(StoreKind.Google, buildNumber, version, buildNumber, Session("1.0.0", "42")).Should().Be(expected);

    [Fact]
    public void Microsoft_ShouldMatchThePackageVersion()
    {
        BuildPackages.Matches(StoreKind.Microsoft, "pkg-1", "1.0.7.0", "1.0.7.0", Session("1.0.7.0", null)).Should().BeTrue();
        BuildPackages.Matches(StoreKind.Microsoft, "pkg-1", "1.0.8.0", "1.0.8.0", Session("1.0.7.0", null)).Should().BeFalse();
    }

    [Fact]
    public void MissingPackageInfo_ShouldNotMatch()
    {
        BuildPackages.Matches(StoreKind.Apple, "x", "1.0", "1", new ArtifactSession()).Should().BeFalse();
        BuildPackages.Matches(StoreKind.Apple, "x", null, null, Session(null, null)).Should().BeFalse();
        BuildPackages.Matches(StoreKind.Apple, "x", "1.0", "1", null).Should().BeFalse();
    }
}
