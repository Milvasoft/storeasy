namespace Storeasy.Application.Interfaces;

/// <summary>
/// Sends the deadline reminders once per threshold: TestFlight build expiry (14, 7 and 1 days before) and the compliance
/// timeline of each app (30, 7 and 1 days before, from the rules catalog). Run daily by a scheduled job.
/// </summary>
public interface IDeadlineNotifier
{
    /// <summary>
    /// Sends the TestFlight build expiry reminders due at <paramref name="now"/>; returns how many were sent.
    /// </summary>
    /// <param name="now"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<int> NotifyBuildExpiryAsync(DateTime now, CancellationToken cancellationToken);

    /// <summary>
    /// Sends the compliance deadline reminders due at <paramref name="now"/>; returns how many were sent.
    /// </summary>
    /// <param name="now"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<int> NotifyComplianceDeadlinesAsync(DateTime now, CancellationToken cancellationToken);
}
