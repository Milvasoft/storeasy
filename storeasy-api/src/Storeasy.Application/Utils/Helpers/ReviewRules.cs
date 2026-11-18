namespace Storeasy.Application.Utils.Helpers;

/// <summary>
/// Store limits of review replies. Google Play publishes 350 characters; App Store Connect publishes no number and the
/// value here is the one the rules catalog keeps (5,970), shown as "from the catalog".
/// </summary>
public static class ReviewRules
{
    /// <summary>
    /// Longest Google Play reply.
    /// </summary>
    public const int GooglePlayReplyMaxLength = 350;

    /// <summary>
    /// Longest App Store reply (catalog value).
    /// </summary>
    public const int AppStoreReplyMaxLength = 5970;

    /// <summary>
    /// Longest reply of <paramref name="store"/>; 0 when replies are not supported there.
    /// </summary>
    public static int ReplyMaxLength(StoreKind store) => store switch
    {
        StoreKind.Apple => AppStoreReplyMaxLength,
        StoreKind.Google => GooglePlayReplyMaxLength,
        _ => 0,
    };

    /// <summary>
    /// Length of a reply as the stores count it (user perceived characters, so an emoji counts once).
    /// </summary>
    public static int Length(string text) => string.IsNullOrEmpty(text) ? 0 : new System.Globalization.StringInfo(text).LengthInTextElements;
}
