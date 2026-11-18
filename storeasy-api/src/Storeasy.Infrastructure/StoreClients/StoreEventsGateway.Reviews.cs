using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;
using Storeasy.Infrastructure.Stores;
using System.Text.Json;

namespace Storeasy.Infrastructure.StoreClients;

/// <summary>
/// Customer reviews: App Store Connect <c>customerReviews</c> and <c>customerReviewResponses</c>, Google Play
/// <c>reviews</c> and <c>reviews:reply</c>.
/// </summary>
public sealed partial class StoreEventsGateway : IStoreReviewGateway
{
    private const int _googlePages = 5;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StoreReviewRecord>> ListReviewsAsync(StoreKind store, string secretJson, string appExternalId, CancellationToken cancellationToken)
    {
        switch (store)
        {
            case StoreKind.Apple:
            {
                var (data, included) = await _client.AppleListAsync($"{AppleBase}/v1/apps/{Uri.EscapeDataString(appExternalId)}/customerReviews?sort=-createdDate&limit=200&include=response"
                                                                    + "&fields[customerReviews]=rating,title,body,reviewerNickname,createdDate,territory,response&fields[customerReviewResponses]=responseBody,lastModifiedDate,state",
                                                                    _client.AppleToken(secretJson),
                                                                    cancellationToken,
                                                                    maxPages: 3);

                return [.. data.Select(r => ParseAppleReview(r, included))];
            }
            case StoreKind.Google:
            {
                var token = await _client.GoogleTokenAsync(secretJson, cancellationToken);
                var reviews = new List<StoreReviewRecord>();
                string pageToken = null;

                for (var page = 0; page < _googlePages; page++)
                {
                    var url = $"{StoreGateway.GoogleBaseUrl}/applications/{Uri.EscapeDataString(appExternalId)}/reviews?maxResults=100" + (pageToken == null ? string.Empty : $"&token={Uri.EscapeDataString(pageToken)}");
                    var root = await _client.JsonAsync(HttpMethod.Get, url, token, null, cancellationToken);

                    reviews.AddRange(StoreApiClient.Items(root, "reviews").Select(ParseGoogleReview).Where(r => r != null));

                    pageToken = StoreApiClient.Text(StoreApiClient.Child(root, "tokenPagination"), "nextPageToken");

                    if (pageToken == null)
                        break;
                }

                return reviews;
            }
            default:
                throw new StoreCallException($"{store} reviews are answered in the store's own console.", null, false);
        }
    }

    /// <inheritdoc/>
    public async Task<StoreReplyRecord> ReplyAsync(StoreKind store, string secretJson, string appExternalId, string reviewExternalId, string existingReplyExternalId, string body, CancellationToken cancellationToken)
    {
        switch (store)
        {
            case StoreKind.Apple:
            {
                var token = _client.AppleToken(secretJson);

                if (!string.IsNullOrEmpty(existingReplyExternalId))
                    await _client.JsonOrNotFoundAsync(HttpMethod.Delete, $"{AppleBase}/v1/customerReviewResponses/{Uri.EscapeDataString(existingReplyExternalId)}", token, null, cancellationToken);

                var created = await _client.JsonAsync(HttpMethod.Post,
                                                      $"{AppleBase}/v1/customerReviewResponses",
                                                      token,
                                                      new { data = new { type = "customerReviewResponses", attributes = new { responseBody = body }, relationships = new { review = new { data = new { type = "customerReviews", id = reviewExternalId } } } } },
                                                      cancellationToken);

                var data = StoreApiClient.Child(created, "data");

                return new StoreReplyRecord(StoreApiClient.Text(data, "id"), StoreApiClient.Date(StoreApiClient.Child(data, "attributes"), "lastModifiedDate") ?? DateTime.UtcNow);
            }
            case StoreKind.Google:
            {
                var token = await _client.GoogleTokenAsync(secretJson, cancellationToken);
                var result = await _client.JsonAsync(HttpMethod.Post,
                                                     $"{StoreGateway.GoogleBaseUrl}/applications/{Uri.EscapeDataString(appExternalId)}/reviews/{Uri.EscapeDataString(reviewExternalId)}:reply",
                                                     token,
                                                     new { replyText = body },
                                                     cancellationToken);

                return new StoreReplyRecord(null, Seconds(StoreApiClient.Child(StoreApiClient.Child(result, "result"), "lastEdited")) ?? DateTime.UtcNow);
            }
            default:
                throw new StoreCallException($"{store} reviews are answered in the store's own console.", null, false);
        }
    }

    /// <summary>
    /// Maps an App Store <c>customerReviews</c> resource with its included response.
    /// </summary>
    public static StoreReviewRecord ParseAppleReview(JsonElement review, List<JsonElement> included)
    {
        var attributes = StoreApiClient.Child(review, "attributes");
        var responseId = StoreApiClient.RelationshipId(review, "response");
        var response = responseId == null ? default : included.FirstOrDefault(i => StoreApiClient.Text(i, "type") == "customerReviewResponses" && StoreApiClient.Text(i, "id") == responseId);
        var responseAttributes = StoreApiClient.Child(response, "attributes");

        return new StoreReviewRecord(StoreApiClient.Text(review, "id"),
                                     StoreApiClient.Text(attributes, "reviewerNickname"),
                                     StoreApiClient.Number(attributes, "rating") ?? 0,
                                     StoreApiClient.Text(attributes, "title"),
                                     StoreApiClient.Text(attributes, "body"),
                                     null,
                                     StoreApiClient.Text(attributes, "territory"),
                                     null,
                                     null,
                                     StoreApiClient.Date(attributes, "createdDate") ?? DateTime.UtcNow,
                                     response.ValueKind == JsonValueKind.Object ? responseId : null,
                                     StoreApiClient.Text(responseAttributes, "responseBody"),
                                     StoreApiClient.Date(responseAttributes, "lastModifiedDate"));
    }

    /// <summary>
    /// Maps a Google Play review (the user comment and the developer comment); null when it has no user comment.
    /// </summary>
    public static StoreReviewRecord ParseGoogleReview(JsonElement review)
    {
        var comments = StoreApiClient.Items(review, "comments").ToList();
        var user = comments.Select(c => StoreApiClient.Child(c, "userComment")).FirstOrDefault(c => c.ValueKind == JsonValueKind.Object);
        var developer = comments.Select(c => StoreApiClient.Child(c, "developerComment")).FirstOrDefault(c => c.ValueKind == JsonValueKind.Object);

        if (user.ValueKind != JsonValueKind.Object)
            return null;

        var device = StoreApiClient.Text(StoreApiClient.Child(user, "deviceMetadata"), "productName") ?? StoreApiClient.Text(user, "device");

        return new StoreReviewRecord(StoreApiClient.Text(review, "reviewId"),
                                     StoreApiClient.Text(review, "authorName"),
                                     StoreApiClient.Number(user, "starRating") ?? 0,
                                     null,
                                     StoreApiClient.Text(user, "text")?.Trim(),
                                     StoreApiClient.Text(user, "reviewerLanguage"),
                                     null,
                                     StoreApiClient.Text(user, "appVersionName"),
                                     device,
                                     Seconds(StoreApiClient.Child(user, "lastModified")) ?? DateTime.UtcNow,
                                     null,
                                     developer.ValueKind == JsonValueKind.Object ? StoreApiClient.Text(developer, "text") : null,
                                     developer.ValueKind == JsonValueKind.Object ? Seconds(StoreApiClient.Child(developer, "lastModified")) : null);
    }

    private static DateTime? Seconds(JsonElement timestamp)
    {
        if (timestamp.ValueKind != JsonValueKind.Object || !timestamp.TryGetProperty("seconds", out var seconds))
            return null;

        var value = seconds.ValueKind == JsonValueKind.String ? long.TryParse(seconds.GetString(), out var parsed) ? parsed : (long?)null : seconds.TryGetInt64(out var number) ? number : null;

        return value.HasValue ? DateTimeOffset.FromUnixTimeSeconds(value.Value).UtcDateTime : null;
    }
}
