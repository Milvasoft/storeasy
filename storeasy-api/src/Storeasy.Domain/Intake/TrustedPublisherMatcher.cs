using Storeasy.Domain.Enums;

namespace Storeasy.Domain.Intake;

/// <summary>
/// Compares a validated OIDC token with a trusted publisher rule (tools/CONTRACT.md 3.1, design 13.5-13.7). GitHub:
/// <c>repository</c>, <c>ref</c> (branch glob, checked separately from <c>sub</c> because an environment job has no branch in
/// <c>sub</c>), <c>environment</c>, optional <c>job_workflow_ref</c>, audience <see cref="StoreasyAudience"/>. Azure DevOps:
/// <c>sub</c> = <c>sc://org/project/service connection</c>, audience <see cref="AzureAudience"/>. GitLab: <c>project_path</c>,
/// <c>ref</c>, <c>ref_type</c>, <c>environment</c>, audience <see cref="StoreasyAudience"/>. No database, no network.
/// </summary>
public static class TrustedPublisherMatcher
{
    /// <summary>
    /// Issuer of GitHub Actions tokens.
    /// </summary>
    public const string GitHubIssuer = "https://token.actions.githubusercontent.com";

    /// <summary>
    /// Issuer prefix of Azure DevOps workload identity tokens (followed by the organization id).
    /// </summary>
    public const string AzureIssuerPrefix = "https://vstoken.dev.azure.com/";

    /// <summary>
    /// Issuer of gitlab.com tokens.
    /// </summary>
    public const string GitLabIssuer = "https://gitlab.com";

    /// <summary>
    /// Audience GitHub and GitLab tokens are requested with.
    /// </summary>
    public const string StoreasyAudience = "storeasy";

    /// <summary>
    /// Fixed audience of Azure DevOps workload identity tokens.
    /// </summary>
    public const string AzureAudience = "api://AzureADTokenExchange";

    /// <summary>
    /// Audience a rule's tokens must carry.
    /// </summary>
    public static string AudienceOf(OidcProvider provider) => provider == OidcProvider.AzureDevOps ? AzureAudience : StoreasyAudience;

    /// <summary>
    /// Whether <paramref name="issuer"/> is an Azure DevOps token issuer (<c>https://vstoken.dev.azure.com/{guid}</c>).
    /// </summary>
    public static bool IsAzureIssuer(string issuer)
        => issuer != null && issuer.StartsWith(AzureIssuerPrefix, StringComparison.Ordinal) && Guid.TryParse(issuer[AzureIssuerPrefix.Length..].TrimEnd('/'), out _);

    /// <summary>
    /// Issuer a rule's tokens come from; for Azure DevOps a pattern (the organization id is not known to the rule).
    /// </summary>
    public static string IssuerOf(TrustedPublisher rule) => rule.Provider switch
    {
        OidcProvider.GitHub => GitHubIssuer,
        OidcProvider.AzureDevOps => AzureIssuerPrefix + "*",
        _ => string.IsNullOrWhiteSpace(rule.Issuer) ? GitLabIssuer : rule.Issuer.TrimEnd('/'),
    };

    /// <summary>
    /// The claims a matching token carries (shown to members when they set up the CI), in display order.
    /// </summary>
    public static Dictionary<string, string> ExpectedClaims(TrustedPublisher rule)
    {
        var claims = new Dictionary<string, string>
        {
            ["iss"] = IssuerOf(rule),
            ["aud"] = AudienceOf(rule.Provider),
        };

        switch (rule.Provider)
        {
            case OidcProvider.GitHub:
                claims["repository"] = rule.Repository;

                if (!string.IsNullOrWhiteSpace(rule.Branch))
                    claims["ref"] = "refs/heads/" + rule.Branch;

                if (!string.IsNullOrWhiteSpace(rule.Environment))
                    claims["environment"] = rule.Environment;

                if (!string.IsNullOrWhiteSpace(rule.Workflow))
                    claims["job_workflow_ref"] = $"{rule.Repository}/{rule.Workflow.TrimStart('/')}@*";

                break;
            case OidcProvider.AzureDevOps:
                claims["sub"] = $"sc://{rule.AzureOrganization}/{rule.AzureProject}/{rule.ServiceConnection}";
                break;
            default:
                claims["project_path"] = rule.Repository;

                if (!string.IsNullOrWhiteSpace(rule.Branch))
                {
                    claims["ref"] = rule.Branch;
                    claims["ref_type"] = "branch";
                }

                if (!string.IsNullOrWhiteSpace(rule.Environment))
                    claims["environment"] = rule.Environment;

                break;
        }

        return claims;
    }

    /// <summary>
    /// Claim by claim comparison of a validated token with <paramref name="rule"/>.
    /// </summary>
    public static List<ClaimComparison> Compare(TrustedPublisher rule, string issuer, IReadOnlyCollection<string> audiences, IReadOnlyDictionary<string, string> claims)
    {
        var result = new List<ClaimComparison>();

        foreach (var (claim, expected) in ExpectedClaims(rule))
        {
            string actual;
            bool matches;

            switch (claim)
            {
                case "iss":
                    actual = issuer;
                    matches = rule.Provider == OidcProvider.AzureDevOps ? IsAzureIssuer(issuer) : string.Equals(issuer?.TrimEnd('/'), expected, StringComparison.Ordinal);
                    break;
                case "aud":
                    actual = string.Join(",", audiences ?? []);
                    matches = audiences?.Contains(expected) == true;
                    break;
                case "ref" when rule.Provider == OidcProvider.GitHub:
                    actual = Get(claims, "ref");
                    matches = actual != null && actual.StartsWith("refs/heads/", StringComparison.Ordinal) && IntakeRules.GlobMatches(rule.Branch, IntakeRules.BranchOf(actual));
                    break;
                case "sub" when rule.Provider == OidcProvider.AzureDevOps:
                    actual = Get(claims, "sub");
                    matches = string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
                    break;
                default:
                    actual = Get(claims, claim);
                    matches = IntakeRules.GlobMatches(expected, actual) && actual != null;
                    break;
            }

            result.Add(new ClaimComparison(claim, expected, actual, matches));
        }

        return result;
    }

    /// <summary>
    /// Whether every claim matches.
    /// </summary>
    public static bool Matches(TrustedPublisher rule, string issuer, IReadOnlyCollection<string> audiences, IReadOnlyDictionary<string, string> claims)
        => Compare(rule, issuer, audiences, claims).TrueForAll(c => c.Matches);

    /// <summary>
    /// Link to the CI run named by the claims, if any.
    /// </summary>
    public static string RunUrlOf(IReadOnlyDictionary<string, string> claims)
    {
        var repository = Get(claims, "repository");
        var runId = Get(claims, "run_id");

        if (repository != null && runId != null)
            return $"https://github.com/{repository}/actions/runs/{runId}";

        var pipelineUrl = Get(claims, "pipeline_url") ?? Get(claims, "job_url");

        return pipelineUrl != null && pipelineUrl.StartsWith("https://", StringComparison.Ordinal) ? pipelineUrl : null;
    }

    private static string Get(IReadOnlyDictionary<string, string> claims, string name) => claims != null && claims.TryGetValue(name, out var value) ? value : null;
}

/// <summary>
/// One claim of a comparison.
/// </summary>
/// <param name="Claim">Claim name.</param>
/// <param name="Expected">Expected value or pattern.</param>
/// <param name="Actual">Value in the token.</param>
/// <param name="Matches">Whether it matches.</param>
public sealed record ClaimComparison(string Claim, string Expected, string Actual, bool Matches);
