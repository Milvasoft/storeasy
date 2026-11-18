namespace Storeasy.Application.Utils.Models;

/// <summary>
/// E-mail delivery settings (configuration section <see cref="SectionName"/>), bound once and registered as a singleton.
/// Secrets (SMTP password, Notifike API key) come only from user secrets or the environment
/// (<c>Storeasy__Email__Smtp__Password</c>, <c>Storeasy__Email__Notifike__ApiKey</c>).
/// </summary>
public class EmailOptions
{
    /// <summary>
    /// Configuration section of the options.
    /// </summary>
    public const string SectionName = "Storeasy:Email";

    /// <summary>
    /// Which sender is used. <see cref="EmailProvider.Log"/> is accepted only in Development.
    /// </summary>
    public EmailProvider Provider { get; set; } = EmailProvider.Smtp;

    /// <summary>
    /// Display name of the sender.
    /// </summary>
    public string FromName { get; set; } = "storeasy";

    /// <summary>
    /// SMTP settings, used when <see cref="Provider"/> is <see cref="EmailProvider.Smtp"/>.
    /// </summary>
    public SmtpEmailOptions Smtp { get; set; } = new();

    /// <summary>
    /// Notifike settings, used when <see cref="Provider"/> is <see cref="EmailProvider.Notifike"/>.
    /// </summary>
    public NotifikeEmailOptions Notifike { get; set; } = new();

    /// <summary>
    /// The outbox and its background sender.
    /// </summary>
    public EmailOutboxOptions Outbox { get; set; } = new();

    /// <summary>
    /// Name of the first missing required setting of the selected provider, or null when it can send.
    /// </summary>
    /// <returns></returns>
    public string FindMissingSetting() => Provider switch
    {
        EmailProvider.Smtp when string.IsNullOrWhiteSpace(Smtp?.Host) => $"{SectionName}:Smtp:Host",
        EmailProvider.Smtp when string.IsNullOrWhiteSpace(Smtp?.From) => $"{SectionName}:Smtp:From",
        EmailProvider.Notifike when string.IsNullOrWhiteSpace(Notifike?.BaseUrl) => $"{SectionName}:Notifike:BaseUrl",
        EmailProvider.Notifike when string.IsNullOrWhiteSpace(Notifike?.ApiKey) => $"{SectionName}:Notifike:ApiKey",
        _ => null,
    };
}

/// <summary>
/// SMTP server settings.
/// </summary>
public class SmtpEmailOptions
{
    /// <summary>
    /// Server host name.
    /// </summary>
    public string Host { get; set; }

    /// <summary>
    /// Server port.
    /// </summary>
    public int Port { get; set; } = 587;

    /// <summary>
    /// Login user name. Empty for an unauthenticated relay.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Login password (secret, set through the environment).
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Sender address.
    /// </summary>
    public string From { get; set; }

    /// <summary>
    /// Whether the connection is upgraded with STARTTLS.
    /// </summary>
    public bool EnableSsl { get; set; } = true;
}

/// <summary>
/// Settings of the e-mail outbox (<c>Storeasy:Email:Outbox</c>): e-mails are queued on the request path and a background
/// sender delivers them.
/// </summary>
public class EmailOutboxOptions
{
    /// <summary>
    /// Whether this instance runs the background sender (every instance can queue).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Pause of the sender when the outbox is empty.
    /// </summary>
    public int PollIntervalMilliseconds { get; set; } = 1000;

    /// <summary>
    /// Attempts of one e-mail before it is dropped (and logged).
    /// </summary>
    public int MaxAttempts { get; set; } = 6;

    /// <summary>
    /// First retry delay; doubles with every attempt, at most an hour.
    /// </summary>
    public int RetryBaseDelaySeconds { get; set; } = 30;

    /// <summary>
    /// How long a claimed e-mail is held by one sender before another may take it over.
    /// </summary>
    public int LeaseSeconds { get; set; } = 120;
}

/// <summary>
/// Notifike API settings.
/// </summary>
public class NotifikeEmailOptions
{
    /// <summary>
    /// API base address, e.g. <c>https://notifike.example.com/api/v1</c>.
    /// </summary>
    public string BaseUrl { get; set; }

    /// <summary>
    /// API key sent in the <c>X-Api-Key</c> header (secret, set through the environment).
    /// </summary>
    public string ApiKey { get; set; }
}
