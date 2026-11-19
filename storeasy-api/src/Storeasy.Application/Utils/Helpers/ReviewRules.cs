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

    /// <summary>
    /// Fits a written reply (an AI draft) into <paramref name="maxLength"/> user perceived characters. A reply that fits is
    /// returned trimmed. A longer one is cut after the last full sentence that fits, when that keeps at least half of the
    /// limit, so the draft never ends mid sentence; otherwise null (the caller reports that no usable draft came back).
    /// </summary>
    public static string FitReply(string text, int maxLength)
    {
        var reply = text?.Trim();

        if (string.IsNullOrEmpty(reply) || maxLength <= 0)
            return null;

        if (Length(reply) <= maxLength)
            return reply;

        var info = new System.Globalization.StringInfo(reply);
        var head = info.SubstringByTextElements(0, maxLength);

        for (var index = head.Length - 1; index > 0; index--)
        {
            if (!_sentenceEnds.Contains(head[index]))
                continue;

            var followedByBreak = index == head.Length - 1 ? char.IsWhiteSpace(reply[head.Length]) : char.IsWhiteSpace(head[index + 1]);

            if (!followedByBreak && !_closingPunctuation.Contains(head[index]))
                continue;

            var cut = head[..(index + 1)].TrimEnd();

            return Length(cut) * 2 >= maxLength ? cut : null;
        }

        return null;
    }

    // Ellipsis and the CJK full stop, exclamation and question marks are written as escapes.
    private static readonly char[] _sentenceEnds = ['.', '!', '?', '\u2026', '\u3002', '\uFF01', '\uFF1F'];

    // Full stops of scripts without spaces between sentences (CJK) end a sentence on their own.
    private static readonly char[] _closingPunctuation = ['\u3002', '\uFF01', '\uFF1F'];
}
