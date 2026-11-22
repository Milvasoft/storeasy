namespace Storeasy.Application.Interfaces;

/// <summary>
/// Replies to user reviews of a channel's products, sent by the shared reply job (<see cref="StoreJobKind.ReplyToReview"/>).
/// A channel whose API can answer reviews registers it next to its <see cref="IStoreChannelReviews"/>; the reply job asks it
/// before the built in review gateway (docs/adding-a-store.md). The reply limit of the store is in
/// <c>ReviewRules.ReplyMaxLength</c>.
/// </summary>
public interface IStoreChannelReplies
{
    /// <summary>
    /// The store whose reviews this answers.
    /// </summary>
    StoreKind Store { get; }

    /// <summary>
    /// Publishes the reply of one review; an existing reply is replaced.
    /// </summary>
    /// <param name="secretJson"></param>
    /// <param name="externalId">The product's id in the store.</param>
    /// <param name="reviewExternalId">The review's id in the store.</param>
    /// <param name="existingReplyExternalId">The current reply's id, when there is one.</param>
    /// <param name="body">Reply text.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<StoreReplyRecord> ReplyAsync(string secretJson, string externalId, string reviewExternalId, string existingReplyExternalId, string body, CancellationToken cancellationToken);
}
