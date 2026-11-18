using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using System.Text.Json;

namespace Storeasy.Infrastructure.StoreClients;

/// <summary>
/// TestFlight: <c>betaGroups</c>, <c>betaTesters</c>, builds with their <c>betaAppReviewSubmission</c> and
/// <c>betaBuildLocalizations</c>, <c>betaAppLocalizations</c> and <c>betaAppReviewSubmissions</c>.
/// </summary>
public sealed partial class StoreEventsGateway : ITestFlightGateway
{
    /// <inheritdoc/>
    public async Task<TestFlightSnapshot> ReadAsync(string secretJson, string appExternalId, CancellationToken cancellationToken)
    {
        var token = _client.AppleToken(secretJson);
        var app = Uri.EscapeDataString(appExternalId);

        var (groupData, _) = await _client.AppleListAsync($"{AppleBase}/v1/apps/{app}/betaGroups?limit=200&fields[betaGroups]=name,isInternalGroup,publicLinkEnabled,publicLink,publicLinkLimit,publicLinkLimitEnabled", token, cancellationToken);

        var groups = new List<BetaGroupRecord>();

        foreach (var group in groupData)
        {
            var groupId = StoreApiClient.Text(group, "id");
            var (groupBuilds, _) = await _client.AppleListAsync($"{AppleBase}/v1/betaGroups/{Uri.EscapeDataString(groupId)}/relationships/builds?limit=200", token, cancellationToken, maxPages: 2);
            var attributes = StoreApiClient.Child(group, "attributes");

            groups.Add(new BetaGroupRecord(groupId,
                                           StoreApiClient.Text(attributes, "name"),
                                           StoreApiClient.Flag(attributes, "isInternalGroup"),
                                           StoreApiClient.Flag(attributes, "publicLinkEnabled"),
                                           StoreApiClient.Text(attributes, "publicLink"),
                                           StoreApiClient.Number(attributes, "publicLinkLimit"),
                                           StoreApiClient.Flag(attributes, "publicLinkLimitEnabled"),
                                           [.. groupBuilds.Select(b => StoreApiClient.Text(b, "id"))]));
        }

        var (testerData, _) = await _client.AppleListAsync($"{AppleBase}/v1/betaTesters?filter[apps]={app}&limit=200&include=betaGroups&fields[betaTesters]=firstName,lastName,email,inviteType,state,betaGroups", token, cancellationToken);

        var testers = testerData.Select(t =>
        {
            var attributes = StoreApiClient.Child(t, "attributes");

            return new BetaTesterRecord(StoreApiClient.Text(t, "id"),
                                        StoreApiClient.Text(attributes, "firstName"),
                                        StoreApiClient.Text(attributes, "lastName"),
                                        StoreApiClient.Text(attributes, "email"),
                                        StoreApiClient.Text(attributes, "inviteType") == "PUBLIC_LINK" ? BetaInviteType.PublicLink : BetaInviteType.Email,
                                        MapTesterState(StoreApiClient.Text(attributes, "state")),
                                        StoreApiClient.RelationshipIds(t, "betaGroups"));
        }).ToList();

        var (buildData, buildIncluded) = await _client.AppleListAsync($"{AppleBase}/v1/builds?filter[app]={app}&limit=50&sort=-uploadedDate&include=betaAppReviewSubmission,betaBuildLocalizations&limit[betaBuildLocalizations]=50"
                                                                      + "&fields[builds]=version,expirationDate,expired,betaAppReviewSubmission,betaBuildLocalizations&fields[betaAppReviewSubmissions]=betaReviewState&fields[betaBuildLocalizations]=locale,whatsNew",
                                                                      token,
                                                                      cancellationToken,
                                                                      maxPages: 2);

        var builds = buildData.Select(b => ParseBetaBuild(b, buildIncluded)).ToList();

        var (localizationData, _) = await _client.AppleListAsync($"{AppleBase}/v1/apps/{app}/betaAppLocalizations?limit=50", token, cancellationToken, maxPages: 1);

        var localizations = localizationData.Select(l =>
        {
            var attributes = StoreApiClient.Child(l, "attributes");

            return new BetaLocalizationRecord(StoreApiClient.Text(l, "id"),
                                              StoreApiClient.Text(attributes, "locale"),
                                              StoreApiClient.Text(attributes, "description"),
                                              StoreApiClient.Text(attributes, "feedbackEmail"),
                                              StoreApiClient.Text(attributes, "marketingUrl"),
                                              StoreApiClient.Text(attributes, "privacyPolicyUrl"));
        }).ToList();

        return new TestFlightSnapshot(groups, testers, builds, localizations);
    }

    /// <inheritdoc/>
    public async Task<string> ApplyAsync(string secretJson, string appExternalId, TestFlightChange change, CancellationToken cancellationToken)
    {
        var token = _client.AppleToken(secretJson);

        switch (change.Operation)
        {
            case TestFlightOperation.CreateGroup:
            {
                var created = await _client.JsonAsync(HttpMethod.Post, $"{AppleBase}/v1/betaGroups", token, new
                {
                    data = new
                    {
                        type = "betaGroups",
                        attributes = new { name = change.Name, isInternalGroup = change.IsInternal, publicLinkEnabled = change.IsInternal ? (bool?)null : change.PublicLinkEnabled, publicLinkLimit = change.PublicLinkLimit, publicLinkLimitEnabled = change.IsInternal ? (bool?)null : change.PublicLinkLimitEnabled },
                        relationships = new { app = new { data = new { type = "apps", id = appExternalId } } },
                    },
                }, cancellationToken);

                return StoreApiClient.Text(StoreApiClient.Child(created, "data"), "id");
            }
            case TestFlightOperation.UpdateGroup:
                await _client.JsonAsync(HttpMethod.Patch, $"{AppleBase}/v1/betaGroups/{Uri.EscapeDataString(change.GroupExternalId)}", token, new
                {
                    data = new { type = "betaGroups", id = change.GroupExternalId, attributes = new { name = change.Name, publicLinkEnabled = change.PublicLinkEnabled, publicLinkLimit = change.PublicLinkLimit, publicLinkLimitEnabled = change.PublicLinkLimitEnabled } },
                }, cancellationToken);

                return change.GroupExternalId;
            case TestFlightOperation.UpdateTesters:
                await UpdateTestersAsync(token, appExternalId, change, cancellationToken);

                return change.GroupExternalId;
            case TestFlightOperation.UpdateBuilds:
            {
                var url = $"{AppleBase}/v1/betaGroups/{Uri.EscapeDataString(change.GroupExternalId)}/relationships/builds";

                if (change.AddBuildExternalIds.Count > 0)
                    await _client.JsonAsync(HttpMethod.Post, url, token, new { data = change.AddBuildExternalIds.Select(id => new { type = "builds", id }) }, cancellationToken);

                if (change.RemoveBuildExternalIds.Count > 0)
                    await _client.JsonAsync(HttpMethod.Delete, url, token, new { data = change.RemoveBuildExternalIds.Select(id => new { type = "builds", id }) }, cancellationToken);

                return change.GroupExternalId;
            }
            case TestFlightOperation.WhatToTest:
                return await UpsertLocalizedAsync(token,
                                                  $"{AppleBase}/v1/builds/{Uri.EscapeDataString(change.BuildExternalId)}/betaBuildLocalizations?limit=50",
                                                  "betaBuildLocalizations",
                                                  change.Locale,
                                                  new { whatsNew = change.Text },
                                                  new { build = new { data = new { type = "builds", id = change.BuildExternalId } } },
                                                  cancellationToken);
            case TestFlightOperation.Localization:
                return await UpsertLocalizedAsync(token,
                                                  $"{AppleBase}/v1/apps/{Uri.EscapeDataString(appExternalId)}/betaAppLocalizations?limit=50",
                                                  "betaAppLocalizations",
                                                  change.Locale,
                                                  new { description = change.Text, feedbackEmail = change.FeedbackEmail, marketingUrl = change.MarketingUrl, privacyPolicyUrl = change.PrivacyPolicyUrl },
                                                  new { app = new { data = new { type = "apps", id = appExternalId } } },
                                                  cancellationToken);
            case TestFlightOperation.SubmitBetaReview:
            {
                var submitted = await _client.JsonAsync(HttpMethod.Post, $"{AppleBase}/v1/betaAppReviewSubmissions", token, new
                {
                    data = new { type = "betaAppReviewSubmissions", relationships = new { build = new { data = new { type = "builds", id = change.BuildExternalId } } } },
                }, cancellationToken);

                return StoreApiClient.Text(StoreApiClient.Child(submitted, "data"), "id");
            }
            default:
                throw new StoreCallException($"Unknown TestFlight operation {change.Operation}.", null, false);
        }
    }

    /// <summary>
    /// Maps a build with its included beta review submission and beta build localizations.
    /// </summary>
    public static BetaBuildRecord ParseBetaBuild(JsonElement build, List<JsonElement> included)
    {
        var attributes = StoreApiClient.Child(build, "attributes");
        var submissionId = StoreApiClient.RelationshipId(build, "betaAppReviewSubmission");
        var localizationIds = StoreApiClient.RelationshipIds(build, "betaBuildLocalizations");

        var submission = submissionId == null ? default : included.FirstOrDefault(i => StoreApiClient.Text(i, "type") == "betaAppReviewSubmissions" && StoreApiClient.Text(i, "id") == submissionId);
        var state = submission.ValueKind == JsonValueKind.Object ? MapReviewState(StoreApiClient.Text(StoreApiClient.Child(submission, "attributes"), "betaReviewState")) : BetaReviewState.NotSubmitted;

        var whatToTest = included.Where(i => StoreApiClient.Text(i, "type") == "betaBuildLocalizations" && localizationIds.Contains(StoreApiClient.Text(i, "id")))
                                 .Select(i => new LocalizedText { Locale = StoreApiClient.Text(StoreApiClient.Child(i, "attributes"), "locale"), Text = StoreApiClient.Text(StoreApiClient.Child(i, "attributes"), "whatsNew") })
                                 .ToList();

        return new BetaBuildRecord(StoreApiClient.Text(build, "id"), state, whatToTest, StoreApiClient.Date(attributes, "expirationDate"), StoreApiClient.Flag(attributes, "expired"));
    }

    private async Task UpdateTestersAsync(string token, string appExternalId, TestFlightChange change, CancellationToken cancellationToken)
    {
        var group = new { type = "betaGroups", id = change.GroupExternalId };

        foreach (var tester in change.AddTesters)
        {
            try
            {
                await _client.JsonAsync(HttpMethod.Post, $"{AppleBase}/v1/betaTesters", token, new
                {
                    data = new { type = "betaTesters", attributes = new { email = tester.Email, firstName = tester.FirstName, lastName = tester.LastName }, relationships = new { betaGroups = new { data = new[] { group } } } },
                }, cancellationToken);
            }
            catch (StoreCallException ex) when (ex.StatusCode == 409)
            {
                // The tester exists (in another group of the account): add the existing tester to the group.
                var (found, _) = await _client.AppleListAsync($"{AppleBase}/v1/betaTesters?filter[email]={Uri.EscapeDataString(tester.Email)}&limit=1", token, cancellationToken, maxPages: 1);
                var testerId = found.Select(f => StoreApiClient.Text(f, "id")).FirstOrDefault() ?? throw ex;

                await _client.JsonAsync(HttpMethod.Post, $"{AppleBase}/v1/betaGroups/{Uri.EscapeDataString(change.GroupExternalId)}/relationships/betaTesters", token, new { data = new[] { new { type = "betaTesters", id = testerId } } }, cancellationToken);
            }
        }

        if (change.RemoveTesterExternalIds.Count > 0)
        {
            await _client.JsonAsync(HttpMethod.Delete,
                                    $"{AppleBase}/v1/betaGroups/{Uri.EscapeDataString(change.GroupExternalId)}/relationships/betaTesters",
                                    token,
                                    new { data = change.RemoveTesterExternalIds.Select(id => new { type = "betaTesters", id }) },
                                    cancellationToken);
        }
    }

    private async Task<string> UpsertLocalizedAsync(string token, string listUrl, string type, string locale, object attributes, object relationships, CancellationToken cancellationToken)
    {
        var (existing, _) = await _client.AppleListAsync(listUrl, token, cancellationToken, maxPages: 1);
        var match = existing.FirstOrDefault(e => string.Equals(StoreApiClient.Text(StoreApiClient.Child(e, "attributes"), "locale"), locale, StringComparison.OrdinalIgnoreCase));

        if (match.ValueKind == JsonValueKind.Object)
        {
            var id = StoreApiClient.Text(match, "id");

            await _client.JsonAsync(HttpMethod.Patch, $"{AppleBase}/v1/{type}/{Uri.EscapeDataString(id)}", token, new { data = new { type, id, attributes } }, cancellationToken);

            return id;
        }

        var merged = JsonSerializer.SerializeToElement(attributes);
        var withLocale = merged.EnumerateObject().ToDictionary(p => p.Name, p => (object)p.Value);
        withLocale["locale"] = locale;

        var created = await _client.JsonAsync(HttpMethod.Post, $"{AppleBase}/v1/{type}", token, new { data = new { type, attributes = withLocale, relationships } }, cancellationToken);

        return StoreApiClient.Text(StoreApiClient.Child(created, "data"), "id");
    }

    private static BetaReviewState MapReviewState(string state) => state switch
    {
        "WAITING_FOR_REVIEW" => BetaReviewState.WaitingForReview,
        "IN_REVIEW" => BetaReviewState.InReview,
        "APPROVED" => BetaReviewState.Approved,
        "REJECTED" => BetaReviewState.Rejected,
        _ => BetaReviewState.NotSubmitted,
    };

    private static BetaTesterState MapTesterState(string state) => state switch
    {
        "INVITED" => BetaTesterState.Invited,
        "ACCEPTED" => BetaTesterState.Accepted,
        "INSTALLED" => BetaTesterState.Installed,
        "REVOKED" => BetaTesterState.Revoked,
        _ => BetaTesterState.NotInvited,
    };
}
