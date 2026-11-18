namespace Storeasy.Application.Utils.Models;

/// <summary>
/// Rule watcher settings (configuration section <see cref="SectionName"/>).
/// </summary>
public class RuleWatcherOptions
{
    /// <summary>
    /// Configuration section of the options.
    /// </summary>
    public const string SectionName = "Storeasy:RuleWatcher";

    /// <summary>
    /// Whether the scheduled watcher runs.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Largest page read, in bytes.
    /// </summary>
    public int MaxBytes { get; set; } = 2 * 1024 * 1024;

    /// <summary>
    /// Official domains the watcher may fetch (a host matches itself and its subdomains).
    /// </summary>
    public List<string> AllowedHosts { get; set; } = ["developer.apple.com", "support.google.com", "developer.android.com", "android-developers.googleblog.com", "learn.microsoft.com"];

    /// <summary>
    /// Sources to watch (added to the database when missing).
    /// </summary>
    public List<RuleWatcherSource> Sources { get; set; } =
    [
        new() { Name = "Apple Developer News", Store = StoreKind.Apple, Url = "https://developer.apple.com/news/" },
        new() { Name = "Apple Developer News RSS", Store = StoreKind.Apple, Url = "https://developer.apple.com/news/rss/news.rss" },
        new() { Name = "Apple upcoming requirements", Store = StoreKind.Apple, Url = "https://developer.apple.com/news/upcoming-requirements/" },
        new() { Name = "App Store Connect release notes", Store = StoreKind.Apple, Url = "https://developer.apple.com/help/app-store-connect/release-notes/" },
        new() { Name = "App Store Connect screenshot specifications", Store = StoreKind.Apple, Url = "https://developer.apple.com/help/app-store-connect/reference/app-information/screenshot-specifications/" },
        new() { Name = "Google Play target API level", Store = StoreKind.Google, Url = "https://support.google.com/googleplay/android-developer/answer/11926878" },
        new() { Name = "Google Play policy announcement", Store = StoreKind.Google, Url = "https://support.google.com/googleplay/android-developer/answer/17134731" },
    ];
}

/// <summary>
/// One configured watcher source.
/// </summary>
public class RuleWatcherSource
{
    /// <summary>
    /// Display name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Store.
    /// </summary>
    public StoreKind Store { get; set; }

    /// <summary>
    /// HTTPS address on an allowed host.
    /// </summary>
    public string Url { get; set; }
}
