using Storeasy.Application.Features.Settings.CompleteSetup;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// First run of a single team instance (<see cref="DeploymentMode.SingleTeam"/>): while the instance has no user, the
/// first visitor who knows the one time setup code creates the administrator and the team.
/// </summary>
public interface IFirstRunSetup
{
    /// <summary>
    /// True while the setup can run: single team mode and no user in the database (soft deleted rows count as users).
    /// Once a user exists the answer is cached as false for the life of the process.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<bool> IsRequiredAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Creates the setup code (or takes <c>Storeasy:Bootstrap:SetupCode</c>) and writes it to the log once, when the
    /// setup is required. Called at startup; safe to call again.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task PrepareAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Compares <paramref name="code"/> with the current setup code in constant time (case, spaces and dashes ignored).
    /// </summary>
    /// <param name="code"></param>
    /// <returns></returns>
    Task<bool> VerifyCodeAsync(string code);

    /// <summary>
    /// In one transaction (serialized across instances): creates the platform administrator (Manager, SuperAdmin role,
    /// verified e-mail) and the single organization with that user as its owner. Returns the user and the organization,
    /// or the message key of the refusal (<see cref="MessageKey.SetupAlreadyCompleted"/> when a user exists).
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<(int UserId, int OrganizationId, string ErrorKey)> CompleteAsync(CompleteSetupCommand request, CancellationToken cancellationToken);
}
