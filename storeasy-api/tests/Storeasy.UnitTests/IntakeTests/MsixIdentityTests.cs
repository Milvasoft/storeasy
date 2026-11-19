using FluentAssertions;
using Storeasy.Domain.Intake;

namespace Storeasy.UnitTests.IntakeTests;

[Trait("Intake Unit Tests", "MSIX package identity: publisher ids, family names and the match of a package with a Microsoft Store record.")]
public class MsixIdentityTests
{
    private const string _kabukPublisher = "CN=934D5176-C6E5-455B-9AAC-3ECA3C954CB4";
    private const string _storeId = "9NGK4T6XHF34";

    private static string KabukFamily => $"Milvasoft.Kabuk_{MsixIdentity.PublisherId(_kabukPublisher)}";

    [Theory]
    [InlineData("CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US", "8wekyb3d8bbwe")]
    [InlineData("CN=Microsoft Windows, O=Microsoft Corporation, L=Redmond, S=Washington, C=US", "cw5n1h2txyewy")]
    public void PublisherId_ShouldBeTheIdWindowsDerivesFromThePublisher(string publisher, string expected)
        => MsixIdentity.PublisherId(publisher).Should().Be(expected);

    [Fact]
    public void FamilyName_ShouldJoinTheNameAndThePublisherId()
    {
        MsixIdentity.FamilyName("Microsoft.WindowsTerminal", "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US").Should().Be("Microsoft.WindowsTerminal_8wekyb3d8bbwe");
        MsixIdentity.FamilyName("Milvasoft.Kabuk", null).Should().Be("Milvasoft.Kabuk", "without a publisher only the identity name is known");
        MsixIdentity.FamilyName(null, _kabukPublisher).Should().BeNull();
    }

    [Fact]
    public void Match_ShouldAcceptTheIdentityNameOfTheStoredFamilyName_AndNeverCompareWithTheStoreId()
    {
        MsixIdentity.Match("Milvasoft.Kabuk", _kabukPublisher, KabukFamily, _storeId).Matches.Should().BeTrue();
        MsixIdentity.Match("milvasoft.kabuk", _kabukPublisher, KabukFamily, _storeId).Matches.Should().BeTrue("Windows package names do not depend on case");
        MsixIdentity.Match("Milvasoft.Kabuk", null, KabukFamily, _storeId).Matches.Should().BeTrue("a package without a read publisher is matched by name");
        MsixIdentity.Match("Milvasoft.Kabuk", _kabukPublisher, "Milvasoft.Kabuk", _storeId).Matches.Should().BeTrue("a stored identity name is matched as is");
    }

    [Fact]
    public void Match_ShouldRefuseAnotherNameOrAnotherPublisher()
    {
        var otherName = MsixIdentity.Match("Milvasoft.Crewhall", _kabukPublisher, KabukFamily, _storeId);
        var otherPublisher = MsixIdentity.Match("Milvasoft.Kabuk", "CN=Someone Else", KabukFamily, _storeId);
        var storeIdAsName = MsixIdentity.Match(_storeId, _kabukPublisher, KabukFamily, _storeId);

        otherName.Matches.Should().BeFalse();
        otherName.Reason.Should().Be($"Package Milvasoft.Crewhall does not match the app ({KabukFamily}).");
        otherPublisher.Matches.Should().BeFalse();
        otherPublisher.Reason.Should().Contain("CN=Someone Else").And.Contain(KabukFamily);
        storeIdAsName.Matches.Should().BeFalse("the Store id is not a package identity");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(_storeId)]
    [InlineData("9ngk4t6xhf34")]
    public void Match_ShouldBeUnknown_WhenTheRecordHasNoPackageIdentityYet(string storedIdentity)
    {
        var match = MsixIdentity.Match("Milvasoft.Kabuk", _kabukPublisher, storedIdentity, _storeId);

        match.Matches.Should().BeNull();
        match.Reason.Should().Contain("sync");
    }
}
