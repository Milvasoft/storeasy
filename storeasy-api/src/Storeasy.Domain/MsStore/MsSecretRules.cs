namespace Storeasy.Domain.MsStore;

/// <summary>
/// Rules of Microsoft Entra client secrets (catalog rule <c>microsoft.api-access.client-secret-lifetime</c>): a secret
/// lives at most 24 months (Microsoft recommends 12) and the reminders start 30 days before it expires.
/// </summary>
public static class MsSecretRules
{
    /// <summary>
    /// Longest lifetime of a client secret, in months.
    /// </summary>
    public const int MaxMonths = 24;

    /// <summary>
    /// Days before the expiry the reminders start.
    /// </summary>
    public const int WarnDaysBefore = 30;

    /// <summary>
    /// Whether <paramref name="expiryDate"/> can be the expiry of a client secret entered at <paramref name="utcNow"/>:
    /// in the future and at most <see cref="MaxMonths"/> months away (one day of slack for time zones).
    /// </summary>
    /// <param name="expiryDate"></param>
    /// <param name="utcNow"></param>
    /// <returns></returns>
    public static bool IsValidExpiry(DateTime? expiryDate, DateTime utcNow)
        => expiryDate.HasValue && expiryDate.Value > utcNow && expiryDate.Value <= utcNow.AddMonths(MaxMonths).AddDays(1);
}
