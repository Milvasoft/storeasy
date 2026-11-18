using Storeasy.Domain.Monetization;

namespace Storeasy.Application.Utils.Monetization;

/// <summary>
/// The store's current products of one app, read live: subscription groups and products as unsaved rows (store ids
/// set, storeasy ids not), plus the app's purchase settings. Planning diffs against it, the approval fingerprints it
/// again, and the sync saves it.
/// </summary>
public sealed class MonetizationSnapshot
{
    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// The store's id of the app.
    /// </summary>
    public string ExternalId { get; set; }

    /// <summary>
    /// What was read.
    /// </summary>
    public MonetizationParts Parts { get; set; }

    /// <summary>
    /// Subscription groups (App Store).
    /// </summary>
    public List<ProductGroup> Groups { get; set; } = [];

    /// <summary>
    /// Products.
    /// </summary>
    public List<StoreProduct> Products { get; set; } = [];

    /// <summary>
    /// App Store Server Notifications production address.
    /// </summary>
    public string NotificationUrl { get; set; }

    /// <summary>
    /// Notification version of the production address.
    /// </summary>
    public string NotificationVersion { get; set; }

    /// <summary>
    /// App Store Server Notifications sandbox address.
    /// </summary>
    public string SandboxNotificationUrl { get; set; }

    /// <summary>
    /// Notification version of the sandbox address.
    /// </summary>
    public string SandboxNotificationVersion { get; set; }

    /// <summary>
    /// The product with product id <paramref name="productId"/>, or null.
    /// </summary>
    /// <param name="productId"></param>
    /// <returns></returns>
    public StoreProduct Product(string productId) => this.Products.FirstOrDefault(p => string.Equals(p.ProductId, productId, StringComparison.Ordinal));

    /// <summary>
    /// The group with reference name <paramref name="referenceName"/> or store id <paramref name="externalId"/>, or null.
    /// </summary>
    /// <param name="referenceName"></param>
    /// <param name="externalId"></param>
    /// <returns></returns>
    public ProductGroup Group(string referenceName, string externalId)
        => this.Groups.FirstOrDefault(g => (externalId != null && g.ExternalId == externalId) || (referenceName != null && string.Equals(g.ReferenceName, referenceName, StringComparison.Ordinal)));
}
