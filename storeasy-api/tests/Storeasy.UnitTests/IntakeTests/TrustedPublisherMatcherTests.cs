using FluentAssertions;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Intake;

namespace Storeasy.UnitTests.IntakeTests;

[Trait("Intake Unit Tests", "Trusted publisher claim matching for GitHub, Azure DevOps and GitLab.")]
public class TrustedPublisherMatcherTests
{
    private static readonly string _azure = TrustedPublisherMatcher.AzureIssuerPrefix + Guid.NewGuid();

    [Fact]
    public void GitHub_ShouldCheckRepositoryBranchEnvironmentAndAudience()
    {
        var rule = new TrustedPublisher { Provider = OidcProvider.GitHub, Repository = "kuzeylab/halka", Branch = "main", Environment = "play-internal", Workflow = ".github/workflows/release.yml" };
        var claims = new Dictionary<string, string>
        {
            ["repository"] = "kuzeylab/halka",
            ["ref"] = "refs/heads/main",
            ["environment"] = "play-internal",
            ["job_workflow_ref"] = "kuzeylab/halka/.github/workflows/release.yml@refs/heads/main",
            ["sub"] = "repo:kuzeylab/halka:environment:play-internal",
        };

        TrustedPublisherMatcher.Matches(rule, TrustedPublisherMatcher.GitHubIssuer, ["storeasy"], claims).Should().BeTrue();
        TrustedPublisherMatcher.Matches(rule, TrustedPublisherMatcher.GitHubIssuer, ["api://AzureADTokenExchange"], claims).Should().BeFalse();
        TrustedPublisherMatcher.Matches(rule, TrustedPublisherMatcher.GitHubIssuer, ["storeasy"], new Dictionary<string, string>(claims) { ["ref"] = "refs/tags/main" }).Should().BeFalse();
        TrustedPublisherMatcher.Matches(rule, TrustedPublisherMatcher.GitHubIssuer, ["storeasy"], new Dictionary<string, string>(claims) { ["repository"] = "kuzeylab/halka-fork" }).Should().BeFalse();
        TrustedPublisherMatcher.Matches(rule, "https://gitlab.com", ["storeasy"], claims).Should().BeFalse();
        TrustedPublisherMatcher.Compare(rule, TrustedPublisherMatcher.GitHubIssuer, ["storeasy"], new Dictionary<string, string>(claims) { ["environment"] = "prod" })
                               .Single(c => !c.Matches).Claim.Should().Be("environment");
    }

    [Fact]
    public void AzureDevOps_ShouldCheckTheServiceConnectionAndTheFixedAudience()
    {
        var rule = new TrustedPublisher { Provider = OidcProvider.AzureDevOps, AzureOrganization = "kuzeylab", AzureProject = "Pusula", ServiceConnection = "storeasy-pusula" };
        var claims = new Dictionary<string, string> { ["sub"] = "sc://kuzeylab/Pusula/storeasy-pusula" };

        TrustedPublisherMatcher.ExpectedClaims(rule)["aud"].Should().Be("api://AzureADTokenExchange");
        TrustedPublisherMatcher.Matches(rule, _azure, ["api://AzureADTokenExchange"], claims).Should().BeTrue();
        TrustedPublisherMatcher.Matches(rule, _azure, ["storeasy"], claims).Should().BeFalse();
        TrustedPublisherMatcher.Matches(rule, _azure, ["api://AzureADTokenExchange"], new Dictionary<string, string> { ["sub"] = "sc://other/Pusula/storeasy-pusula" }).Should().BeFalse();
        TrustedPublisherMatcher.Matches(rule, "https://vstoken.dev.azure.com/not-a-guid", ["api://AzureADTokenExchange"], claims).Should().BeFalse();
    }

    [Fact]
    public void GitLab_ShouldUseTheSelfManagedIssuerWhenSet()
    {
        var rule = new TrustedPublisher { Provider = OidcProvider.GitLab, Repository = "kuzeylab/mola", Branch = "release/*", Issuer = "https://git.kuzeylab.dev" };
        var claims = new Dictionary<string, string> { ["project_path"] = "kuzeylab/mola", ["ref"] = "release/3.1", ["ref_type"] = "branch" };

        TrustedPublisherMatcher.Matches(rule, "https://git.kuzeylab.dev", ["storeasy"], claims).Should().BeTrue();
        TrustedPublisherMatcher.Matches(rule, "https://gitlab.com", ["storeasy"], claims).Should().BeFalse();
        TrustedPublisherMatcher.Matches(rule, "https://git.kuzeylab.dev", ["storeasy"], new Dictionary<string, string>(claims) { ["ref_type"] = "tag" }).Should().BeFalse();
        TrustedPublisherMatcher.RunUrlOf(new Dictionary<string, string> { ["repository"] = "kuzeylab/halka", ["run_id"] = "318" }).Should().Be("https://github.com/kuzeylab/halka/actions/runs/318");
    }
}
