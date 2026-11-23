using System.Text.RegularExpressions;

namespace Storeasy.Application.Utils.Registries;

/// <summary>
/// Token expiry reminders (docs/package-registries.md): when to warn about a recorded token and how to refuse a token
/// value. storeasy stores no registry token, so a label or a "used in" text that looks like one is refused.
/// </summary>
public static partial class TokenReminderRules
{
    /// <summary>
    /// Days before the expiry of the first warning.
    /// </summary>
    public const int FirstWarningDays = 14;

    /// <summary>
    /// Days before the expiry of the second warning.
    /// </summary>
    public const int SecondWarningDays = 3;

    /// <summary>
    /// No warning yet.
    /// </summary>
    public const byte StageNone = 0;

    /// <summary>
    /// The 14 day warning.
    /// </summary>
    public const byte StageFirst = 1;

    /// <summary>
    /// The 3 day warning.
    /// </summary>
    public const byte StageSecond = 2;

    /// <summary>
    /// The expiry warning.
    /// </summary>
    public const byte StageExpired = 3;

    // npm_, pypi-, NuGet (oy2), crates.io (cio), RubyGems, GitHub and GitLab tokens, private keys.
    [GeneratedRegex(@"(^|[\s:=])(npm_[A-Za-z0-9]{16,}|pypi-[A-Za-z0-9_-]{16,}|oy2[a-z0-9]{20,}|cio[A-Za-z0-9]{20,}|rubygems_[a-f0-9]{16,}|gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|glpat-[A-Za-z0-9_-]{16,})", RegexOptions.IgnoreCase)]
    private static partial Regex KnownToken();

    // A long run of base64 or hex characters with letters and digits mixed, the shape of most secrets.
    [GeneratedRegex(@"[A-Za-z0-9+=_-]{32,}")]
    private static partial Regex LongSecret();

    /// <summary>
    /// Days from <paramref name="today"/> to the expiry day (negative after it).
    /// </summary>
    public static int DaysLeft(DateTime expiryDate, DateTime today) => (expiryDate.Date - today.Date).Days;

    /// <summary>
    /// The warning stage on <paramref name="today"/>: expired on or after the expiry day, then 3 and 14 days before.
    /// </summary>
    public static byte Stage(DateTime expiryDate, DateTime today)
    {
        var days = DaysLeft(expiryDate, today);

        if (days <= 0)
            return StageExpired;

        if (days <= SecondWarningDays)
            return StageSecond;

        return days <= FirstWarningDays ? StageFirst : StageNone;
    }

    /// <summary>
    /// What the daily check does with one reminder: open its alert when it comes within 14 days, warn (audit and notify) once
    /// per stage, and resolve the alert when the expiry moved out of the 14 days.
    /// </summary>
    /// <param name="warnedStage">The last stage warned about.</param>
    /// <param name="hasOpenAlert">Whether the reminder has an open alert.</param>
    /// <param name="expiryDate">Expiry day.</param>
    /// <param name="today">Today (UTC).</param>
    public static ReminderDecision Decide(byte warnedStage, bool hasOpenAlert, DateTime expiryDate, DateTime today)
    {
        var stage = Stage(expiryDate, today);

        return stage == StageNone
            ? new ReminderDecision(false, hasOpenAlert, false, StageNone)
            : new ReminderDecision(!hasOpenAlert, false, stage > warnedStage, Math.Max(stage, warnedStage));
    }

    /// <summary>
    /// Whether <paramref name="text"/> looks like a token value (a known token prefix or a long secret like string).
    /// </summary>
    public static bool LooksLikeToken(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (KnownToken().IsMatch(text))
            return true;

        return LongSecret().Matches(text).Any(m => m.Value.Any(char.IsDigit) && m.Value.Any(char.IsLetter));
    }
}

/// <summary>
/// Result of <see cref="TokenReminderRules.Decide"/>.
/// </summary>
/// <param name="Open">Open a token expiry alert.</param>
/// <param name="Resolve">Resolve the open alert.</param>
/// <param name="Warn">Audit and notify the new stage.</param>
/// <param name="Stage">The stage to remember.</param>
public sealed record ReminderDecision(bool Open, bool Resolve, bool Warn, byte Stage);
