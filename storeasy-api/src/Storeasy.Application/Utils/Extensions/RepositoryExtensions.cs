using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Utils.Extensions;

/// <summary>
/// Contains extension methods for repositories.
/// </summary>
public static class RepositoryExtensions
{
    /// <summary>
    /// Deletes every session of <paramref name="user"/> and archives the ones it carries (<see cref="User.Sessions"/>)
    /// to the session history. A user without loaded sessions (null or empty) only gets the delete.
    /// Call it inside the transaction that changes the user, and rotate the security stamp after the commit.
    /// </summary>
    /// <param name="userSessionRepository"></param>
    /// <param name="userSessionHistoriesRepository"></param>
    /// <param name="user"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task RevokeAllSessionsAsync(this IStoreasyRepositoryBase<UserSession> userSessionRepository,
                                                    IStoreasyRepositoryBase<UserSessionHistory> userSessionHistoriesRepository,
                                                    User user,
                                                    CancellationToken cancellationToken)
    {
        await userSessionRepository.ExecuteDeleteAsync(UserSession.Conditions.DeleteAllSessions(user.UserName), cancellationToken: cancellationToken);

        if (user.Sessions?.Count > 0)
            await userSessionHistoriesRepository.BulkAddAsync([.. user.Sessions.Select(s => new UserSessionHistory(s))], cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Uses a one time account token: the token must match <paramref name="purpose"/>, be unused and unexpired. The use
    /// is a conditional update, so two concurrent requests with the same token cannot both succeed.
    /// </summary>
    /// <param name="tokenRepository"></param>
    /// <param name="token">The token as received (only its hash is compared).</param>
    /// <param name="purpose"></param>
    /// <param name="now"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>The owner's user id, or null when the token is unknown, expired, used or of another purpose.</returns>
    public static async Task<int?> ConsumeAccountTokenAsync(this IStoreasyRepositoryBase<AccountToken> tokenRepository,
                                                            string token,
                                                            AccountTokenPurpose purpose,
                                                            DateTime now,
                                                            CancellationToken cancellationToken)
    {
        var tokenHash = SecureTokens.Hash(token);

        var found = await tokenRepository.GetFirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.Purpose == purpose && t.UseDate == null && t.ExpiryDate > now,
                                                                 projection: t => new { t.Id, t.UserId },
                                                                 cancellationToken: cancellationToken);

        if (found == null)
            return null;

        var used = await tokenRepository.ExecuteUpdateAsync(t => t.Id == found.Id && t.UseDate == null,
                                                            new SetPropertyBuilder<AccountToken>().SetPropertyValue(t => t.UseDate, (DateTime?)now),
                                                            cancellationToken);

        return used == 0 ? null : found.UserId;
    }

    /// <summary>
    /// Retires every unused token of <paramref name="purpose"/> of the user (a new link replaces the old ones, and a
    /// password reset kills the reset links still in the mailbox).
    /// </summary>
    /// <param name="tokenRepository"></param>
    /// <param name="userId"></param>
    /// <param name="purpose"></param>
    /// <param name="now"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static Task<int> RetireAccountTokensAsync(this IStoreasyRepositoryBase<AccountToken> tokenRepository,
                                                     int userId,
                                                     AccountTokenPurpose purpose,
                                                     DateTime now,
                                                     CancellationToken cancellationToken)
        => tokenRepository.ExecuteUpdateAsync(t => t.UserId == userId && t.Purpose == purpose && t.UseDate == null,
                                              new SetPropertyBuilder<AccountToken>().SetPropertyValue(t => t.UseDate, (DateTime?)now),
                                              cancellationToken);

    /// <summary>
    /// Consumes a pending invitation and adds the membership it grants. The invitation is consumed first with a
    /// conditional update, so two concurrent accepts of the same invitation cannot both add a member. Run it inside a
    /// transaction when the caller writes more (registration). The caller invalidates the cached role and writes the
    /// audit entry.
    /// </summary>
    /// <param name="invitationRepository"></param>
    /// <param name="memberRepository"></param>
    /// <param name="invitationId"></param>
    /// <param name="organizationId"></param>
    /// <param name="userId"></param>
    /// <param name="role"></param>
    /// <param name="now"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>The new membership id, or null when the invitation was consumed meanwhile.</returns>
    public static async Task<int?> JoinOrganizationAsync(this IStoreasyRepositoryBase<OrganizationInvitation> invitationRepository,
                                                         IStoreasyRepositoryBase<OrganizationMember> memberRepository,
                                                         int invitationId,
                                                         int organizationId,
                                                         int userId,
                                                         OrgRole role,
                                                         DateTime now,
                                                         CancellationToken cancellationToken)
    {
        var consumed = await invitationRepository.ExecuteUpdateAsync(i => i.Id == invitationId && i.AcceptDate == null,
                                                                     new SetPropertyBuilder<OrganizationInvitation>().SetPropertyValue(i => i.AcceptDate, now),
                                                                     cancellationToken);

        if (consumed == 0)
            return null;

        var member = new OrganizationMember { OrganizationId = organizationId, UserId = userId, Role = role };

        await memberRepository.AddAsync(member, cancellationToken);

        return member.Id;
    }
}
