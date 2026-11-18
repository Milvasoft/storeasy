namespace Storeasy.Application.Utils.Models;

/// <summary>
/// Settings of the store content engine (configuration section <see cref="SectionName"/>): retry and backoff of App Store
/// Connect and Google Play calls, processing waits, upload chunking and plan lifetime. Bound once and registered as a
/// singleton.
/// </summary>
public class StoreApiOptions
{
    /// <summary>
    /// Configuration section of the options.
    /// </summary>
    public const string SectionName = "Storeasy:Stores";

    /// <summary>
    /// Retries of a call the store answered with 429 or 5xx (or that did not reach it). Creating calls are retried only
    /// where a retry is known to be safe.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// First backoff delay; doubled per retry. A <c>Retry-After</c> header wins.
    /// </summary>
    public int RetryBaseDelayMilliseconds { get; set; } = 800;

    /// <summary>
    /// Longest backoff delay.
    /// </summary>
    public int RetryMaxDelaySeconds { get; set; } = 60;

    /// <summary>
    /// Pause between two reads of an uploaded screenshot's processing state.
    /// </summary>
    public int AssetPollIntervalMilliseconds { get; set; } = 2000;

    /// <summary>
    /// Longest wait for Apple to process an uploaded screenshot; after it the job reports the state instead of failing.
    /// </summary>
    public int AssetPollTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Chunk size of resumable bundle uploads (a multiple of 256 KiB).
    /// </summary>
    public int UploadChunkBytes { get; set; } = 8 * 1024 * 1024;

    /// <summary>
    /// How long a pending change plan can be approved.
    /// </summary>
    public int PlanLifetimeMinutes { get; set; } = 48 * 60;

    /// <summary>
    /// Folder of the rules catalog (<c>catalog.json</c> and the store files). Empty: the copy next to the application,
    /// then the embedded copy.
    /// </summary>
    public string RulesPath { get; set; }
}
