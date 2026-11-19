using FluentAssertions;
using Storeasy.Infrastructure.Stores.Microsoft;

namespace Storeasy.UnitTests.InfrastructureTests;

[Trait("Infrastructure Unit Tests", "Which icon a Microsoft Store logo may replace in the MsSync job.")]
public class MsSyncIconRuleTests
{
    [Theory]
    [InlineData(null, true, true)]
    [InlineData("", true, true)]
    [InlineData("https://is1-ssl.mzstatic.com/image/thumb/a/512x512bb.png", false, true)]
    [InlineData("https://store-images.s-microsoft.com/image/apps.1.old", true, true)]
    [InlineData("https://is1-ssl.mzstatic.com/image/thumb/a/512x512bb.png", true, false)]
    [InlineData("https://play-lh.googleusercontent.com/abc", true, false)]
    [InlineData("not a url", true, false)]
    public void MicrosoftIconMayReplace_ShouldKeepAppStoreAndGooglePlayIcons(string current, bool hasOtherStoreRecord, bool expected)
    {
        MsSyncJobHandler.MicrosoftIconMayReplace(current, hasOtherStoreRecord).Should().Be(expected);
    }
}
