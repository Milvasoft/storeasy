namespace Storeasy.Domain.JsonModels;

/// <summary>
/// A localization of a store product or subscription group as the store has it.
/// </summary>
public class ProductLocalization
{
    /// <summary>
    /// Store's id of the localization (App Store); null on Play.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Locale (en-US, tr).
    /// </summary>
    public string Locale { get; set; }

    /// <summary>
    /// Display name (App Store), title (Play).
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Custom app name of a subscription group localization (null: the app's own name is used).
    /// </summary>
    public string CustomAppName { get; set; }

    /// <summary>
    /// Benefits of a Play subscription listing.
    /// </summary>
    public List<string> Benefits { get; set; }

    /// <summary>
    /// Store state of the localization (the state of the App Store version it belongs to).
    /// </summary>
    public string State { get; set; }
}
