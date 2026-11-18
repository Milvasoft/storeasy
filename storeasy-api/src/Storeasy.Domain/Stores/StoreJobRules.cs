using Storeasy.Domain.Enums;

namespace Storeasy.Domain.Stores;

/// <summary>
/// Pure rules of the store job queue. No database.
/// </summary>
public static class StoreJobRules
{
    /// <summary>
    /// Whether <paramref name="status"/> is final (the job will never run again).
    /// </summary>
    /// <param name="status"></param>
    /// <returns></returns>
    public static bool IsFinished(StoreJobStatus status) => status is StoreJobStatus.Succeeded or StoreJobStatus.Failed or StoreJobStatus.Cancelled;

    /// <summary>
    /// Delay before the next attempt after attempt number <paramref name="attempt"/> (from 1) failed: exponential
    /// (<paramref name="baseDelay"/>, twice that, four times...) and capped at <paramref name="maxDelay"/>.
    /// </summary>
    /// <param name="attempt"></param>
    /// <param name="baseDelay"></param>
    /// <param name="maxDelay"></param>
    /// <returns></returns>
    public static TimeSpan RetryDelay(int attempt, TimeSpan baseDelay, TimeSpan maxDelay)
    {
        if (baseDelay <= TimeSpan.Zero)
            return TimeSpan.Zero;

        var exponent = Math.Clamp(attempt - 1, 0, 20);
        var delay = TimeSpan.FromTicks(baseDelay.Ticks * (1L << exponent));

        return delay > maxDelay ? maxDelay : delay;
    }

    /// <summary>
    /// Whether a failed attempt number <paramref name="attempt"/> is retried: only transient failures, and only while
    /// attempts remain.
    /// </summary>
    /// <param name="attempt"></param>
    /// <param name="maxAttempts"></param>
    /// <param name="transient"></param>
    /// <returns></returns>
    public static bool ShouldRetry(int attempt, int maxAttempts, bool transient) => transient && attempt < maxAttempts;
}
