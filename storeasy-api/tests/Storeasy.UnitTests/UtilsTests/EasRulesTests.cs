using FluentAssertions;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Eas;
using Storeasy.Domain.Enums;

namespace Storeasy.UnitTests.UtilsTests;

[Trait("Utils Unit Tests", "EAS builds: Expo state names, running states, the typed confirmation, git refs and profiles, eas.json profiles, expo.dev addresses and project suggestions.")]
public class EasRulesTests
{
    [Theory]
    [InlineData("NEW", EasBuildStatus.New)]
    [InlineData("IN_QUEUE", EasBuildStatus.InQueue)]
    [InlineData("IN_PROGRESS", EasBuildStatus.InProgress)]
    [InlineData("PENDING_CANCEL", EasBuildStatus.PendingCancel)]
    [InlineData("FINISHED", EasBuildStatus.Finished)]
    [InlineData("ERRORED", EasBuildStatus.Errored)]
    [InlineData("CANCELED", EasBuildStatus.Canceled)]
    public void StatusOf_ShouldMapExposBuildStatus(string status, EasBuildStatus expected) => EasRules.StatusOf(status).Should().Be(expected);

    [Fact]
    public void States_ShouldTellRunningAndCancelableBuilds()
    {
        EasRules.IsActive(EasBuildStatus.Requested).Should().BeTrue();
        EasRules.IsActive(EasBuildStatus.InProgress).Should().BeTrue();
        EasRules.IsActive(EasBuildStatus.Finished).Should().BeFalse();
        EasRules.IsActive(EasBuildStatus.NotStarted).Should().BeFalse();
        EasRules.IsCancelable(EasBuildStatus.InQueue).Should().BeTrue();
        EasRules.IsCancelable(EasBuildStatus.Requested).Should().BeFalse("Expo has not created the build yet");
        EasRules.IsCancelable(EasBuildStatus.Canceled).Should().BeFalse();
    }

    [Fact]
    public void Platforms_ShouldExpandAndMapToExpo()
    {
        EasRules.Expand(EasPlatform.All).Should().Equal(EasPlatform.Ios, EasPlatform.Android);
        EasRules.Expand(EasPlatform.Ios).Should().Equal(EasPlatform.Ios);
        EasRules.ExpoPlatform(EasPlatform.Ios).Should().Be("IOS");
        EasRules.ExpoPlatform(EasPlatform.Android).Should().Be("ANDROID");
        EasRules.PlatformOf("IOS").Should().Be(EasPlatform.Ios);
    }

    [Fact]
    public void Confirmation_ShouldBeTheAppNameAndThePlatformWord()
    {
        EasRules.ConfirmText("Pusula", EasPlatform.Ios).Should().Be("Pusula iOS");
        EasRules.ConfirmText("Pusula", EasPlatform.All).Should().Be("Pusula all");
        EasRules.ConfirmationMatches("Pusula Android", " Pusula Android ").Should().BeTrue();
        EasRules.ConfirmationMatches("Pusula Android", "pusula android").Should().BeFalse();
        EasRules.ConfirmationMatches("Pusula Android", null).Should().BeFalse();
    }

    [Theory]
    [InlineData(EasPlatform.Android, "production", "main", null)]
    [InlineData(EasPlatform.Ios, "store-internal", "release/1.4", null)]
    [InlineData(EasPlatform.Ios, "production", "4f2a9c1", null)]
    [InlineData(EasPlatform.Ios, "bad profile", "main", MessageKey.EasProfileInvalid)]
    [InlineData(EasPlatform.Ios, "production", "../etc", MessageKey.EasGitRefInvalid)]
    [InlineData(EasPlatform.Ios, "production", "-x", MessageKey.EasGitRefInvalid)]
    [InlineData(EasPlatform.Ios, "production", "", MessageKey.EasGitRefInvalid)]
    [InlineData((EasPlatform)0, "production", "main", MessageKey.EasPlatformInvalid)]
    public void StartProblem_ShouldCheckPlatformProfileAndRef(EasPlatform platform, string profile, string gitRef, string problem)
        => EasRules.StartProblem(platform, profile, gitRef).Should().Be(problem);

    [Fact]
    public void ProfilesOf_ShouldReadTheBuildProfilesOfEasJson()
    {
        const string easJson = """
            {
              // eas.json allows comments in practice
              "cli": { "version": ">= 12.0.0" },
              "build": {
                "development": { "developmentClient": true, "distribution": "internal" },
                "preview": { "distribution": "internal" },
                "production": { "autoIncrement": true },
              },
              "submit": { "production": {} }
            }
            """;

        EasRules.ProfilesOf(easJson).Should().Equal("development", "preview", "production");
        EasRules.ProfilesOf("""{ "cli": {} }""").Should().BeNull();
        EasRules.ProfilesOf("not json").Should().BeNull();
    }

    [Fact]
    public void Addresses_ShouldPointAtExpoDev()
    {
        EasRules.BuildPageUrl("https://expo.dev/", "kuzey", "mola", "b1").Should().Be("https://expo.dev/accounts/kuzey/projects/mola/builds/b1");
        EasRules.BuildPageUrl("https://expo.dev", null, "mola", "b1").Should().BeNull();
        EasRules.GitHubSettingsUrl("https://expo.dev", "kuzey", "mola").Should().Be("https://expo.dev/accounts/kuzey/projects/mola/github");
        EasRules.Context(7).Should().Be("ExpoConnection:7");
    }

    [Theory]
    [InlineData("https://expo.dev/artifacts/eas/abc.aab", ".aab")]
    [InlineData("https://expo.dev/artifacts/eas/abc.IPA", ".ipa")]
    [InlineData("https://expo.dev/artifacts/eas/abc.apk", ".apk")]
    [InlineData("https://expo.dev/artifacts/eas/abc.tar.gz", null)]
    [InlineData("not a url", null)]
    public void PackageExtension_ShouldAcceptStorePackagesOnly(string url, string extension) => EasRules.PackageExtension(url).Should().Be(extension);

    [Fact]
    public void MatchScore_ShouldPreferTheBundleIdThenTheName()
    {
        EasRules.MatchScore("Mola", ["com.kuzey.mola"], "other", "Other", "com.kuzey.mola").Should().Be(3);
        EasRules.MatchScore("Mola Timer", [], "mola-timer", "Mola Timer", null).Should().Be(2);
        EasRules.MatchScore("Mola", [], "pusula", "Pusula", null).Should().Be(0);
    }
}
