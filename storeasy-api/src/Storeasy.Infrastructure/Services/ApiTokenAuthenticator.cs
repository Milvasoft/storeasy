using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Intake;
using Storeasy.Domain.Tokens;

namespace Storeasy.Infrastructure.Services;

/// <summary>
/// Default <see cref="IApiTokenAuthenticator"/>. One indexed read by hash per request; a personal access token also
/// needs its owner's current membership (cached for a minute by <see cref="IOrganizationAccess"/>), so removing a member
/// or lowering a role takes effect on their tokens at once. The last use is written only when it is older than
/// <see cref="LastUseInterval"/>, with a conditional update so concurrent requests write once.
/// </summary>
/// <param name="personalTokenRepository"></param>
/// <param name="ciTokenRepository"></param>
/// <param name="uploadTokenRepository"></param>
/// <param name="organizationAccess"></param>
/// <param name="timeProvider"></param>
public sealed class ApiTokenAuthenticator(IStoreasyRepositoryBase<PersonalAccessToken> personalTokenRepository,
                                          IStoreasyRepositoryBase<CiToken> ciTokenRepository,
                                          IStoreasyRepositoryBase<UploadToken> uploadTokenRepository,
                                          IOrganizationAccess organizationAccess,
                                          TimeProvider timeProvider) : IApiTokenAuthenticator
{
    /// <summary>
    /// Least time between two last use writes of one token.
    /// </summary>
    public static readonly TimeSpan LastUseInterval = TimeSpan.FromMinutes(5);

    private const int _clientNameMaxLength = 200;

    private readonly IStoreasyRepositoryBase<PersonalAccessToken> _personalTokenRepository = personalTokenRepository;
    private readonly IStoreasyRepositoryBase<CiToken> _ciTokenRepository = ciTokenRepository;
    private readonly IStoreasyRepositoryBase<UploadToken> _uploadTokenRepository = uploadTokenRepository;
    private readonly IOrganizationAccess _organizationAccess = organizationAccess;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public Task<ApiTokenAuthentication> AuthenticateAsync(string token, string clientName, CancellationToken cancellationToken)
    {
        var kind = TokenPrefixes.KindOf(token);

        if (kind == null || token.Length > 200)
            return Task.FromResult(ApiTokenAuthentication.Failure(MessageKey.Unauthorized));

        var hash = SecureTokens.Hash(token);
        var client = clientName is { Length: > _clientNameMaxLength } ? clientName[.._clientNameMaxLength] : clientName;

        return kind switch
        {
            TokenKind.PersonalAccessToken => AuthenticatePersonalAsync(hash, client, cancellationToken),
            TokenKind.CiToken => AuthenticateCiAsync(hash, client, cancellationToken),
            _ => AuthenticateUploadAsync(hash, cancellationToken),
        };
    }

    private async Task<ApiTokenAuthentication> AuthenticatePersonalAsync(string hash, string client, CancellationToken cancellationToken)
    {
        var row = await _personalTokenRepository.GetFirstOrDefaultAsync(t => t.TokenHash == hash,
                                                                        projection: t => new
                                                                        {
                                                                            t.Id,
                                                                            t.UserId,
                                                                            t.OrganizationId,
                                                                            t.Name,
                                                                            t.Prefix,
                                                                            t.ReadOnly,
                                                                            t.ExpiryDate,
                                                                            t.RevokeDate,
                                                                            t.LastUseDate,
                                                                            t.User.UserName,
                                                                            t.User.UserType,
                                                                            UserDeleted = t.User.IsDeleted,
                                                                            OrganizationDeleted = t.Organization.IsDeleted,
                                                                        },
                                                                        cancellationToken: cancellationToken);

        var now = Now();

        if (row == null || row.UserDeleted || row.OrganizationDeleted)
            return ApiTokenAuthentication.Failure(MessageKey.Unauthorized);

        if (row.RevokeDate != null)
            return ApiTokenAuthentication.Failure(MessageKey.TokenRevoked);

        if (row.ExpiryDate <= now)
            return ApiTokenAuthentication.Failure(MessageKey.TokenExpired);

        var role = await _organizationAccess.GetRoleAsync(row.OrganizationId, row.UserId, cancellationToken);

        if (role == null)
            return ApiTokenAuthentication.Failure(MessageKey.Unauthorized);

        if (row.LastUseDate == null || row.LastUseDate < now - LastUseInterval)
        {
            var threshold = now - LastUseInterval;

            await _personalTokenRepository.ExecuteUpdateAsync(t => t.Id == row.Id && (t.LastUseDate == null || t.LastUseDate < threshold),
                                                              new SetPropertyBuilder<PersonalAccessToken>().SetPropertyValue(t => t.LastUseDate, (DateTime?)now)
                                                                                                           .SetPropertyValue(t => t.LastClientName, client),
                                                              cancellationToken);
        }

        return new ApiTokenAuthentication
        {
            Kind = TokenKind.PersonalAccessToken,
            TokenId = row.Id,
            OrganizationId = row.OrganizationId,
            UserId = row.UserId,
            UserName = row.UserName,
            UserType = row.UserType,
            ReadOnly = row.ReadOnly,
            AllApps = true,
            Prefix = row.Prefix,
            Name = row.Name,
        };
    }

    private async Task<ApiTokenAuthentication> AuthenticateCiAsync(string hash, string client, CancellationToken cancellationToken)
    {
        var row = await _ciTokenRepository.GetFirstOrDefaultAsync(t => t.TokenHash == hash,
                                                                  projection: t => new
                                                                  {
                                                                      t.Id,
                                                                      t.OrganizationId,
                                                                      t.Name,
                                                                      t.Prefix,
                                                                      t.AllApps,
                                                                      t.AppIds,
                                                                      t.Scopes,
                                                                      t.ExpiryDate,
                                                                      t.RevokeDate,
                                                                      t.LastUseDate,
                                                                      OrganizationDeleted = t.Organization.IsDeleted,
                                                                  },
                                                                  cancellationToken: cancellationToken);

        var now = Now();

        if (row == null || row.OrganizationDeleted)
            return ApiTokenAuthentication.Failure(MessageKey.Unauthorized);

        if (row.RevokeDate != null)
            return ApiTokenAuthentication.Failure(MessageKey.TokenRevoked);

        if (row.ExpiryDate <= now)
            return ApiTokenAuthentication.Failure(MessageKey.CiTokenExpired);

        if (row.Scopes?.Contains(CiToken.ArtifactsWriteScope) != true)
            return ApiTokenAuthentication.Failure(MessageKey.Unauthorized);

        if (row.LastUseDate == null || row.LastUseDate < now - LastUseInterval)
        {
            var threshold = now - LastUseInterval;

            await _ciTokenRepository.ExecuteUpdateAsync(t => t.Id == row.Id && (t.LastUseDate == null || t.LastUseDate < threshold),
                                                        new SetPropertyBuilder<CiToken>().SetPropertyValue(t => t.LastUseDate, (DateTime?)now)
                                                                                         .SetPropertyValue(t => t.LastUseSource, client),
                                                        cancellationToken);
        }

        return new ApiTokenAuthentication
        {
            Kind = TokenKind.CiToken,
            TokenId = row.Id,
            OrganizationId = row.OrganizationId,
            UserName = $"ci:{row.Name}",
            AllApps = row.AllApps,
            AppIds = row.AppIds ?? [],
            Prefix = row.Prefix,
            Name = row.Name,
        };
    }

    private async Task<ApiTokenAuthentication> AuthenticateUploadAsync(string hash, CancellationToken cancellationToken)
    {
        var row = await _uploadTokenRepository.GetFirstOrDefaultAsync(t => t.TokenHash == hash,
                                                                      projection: t => new { t.Id, t.OrganizationId, t.AppId, t.Prefix, t.ExpiryDate, t.TrustedPublisherId },
                                                                      cancellationToken: cancellationToken);

        if (row == null)
            return ApiTokenAuthentication.Failure(MessageKey.Unauthorized);

        if (row.ExpiryDate <= Now())
            return ApiTokenAuthentication.Failure(MessageKey.TokenExpired);

        return new ApiTokenAuthentication
        {
            Kind = TokenKind.UploadToken,
            TokenId = row.Id,
            OrganizationId = row.OrganizationId,
            UserName = $"oidc:{row.TrustedPublisherId}",
            AppIds = [row.AppId],
            Prefix = row.Prefix,
            Name = "upload token",
            TrustedPublisherId = row.TrustedPublisherId,
        };
    }

    private DateTime Now() => _timeProvider.GetUtcNow().UtcDateTime;
}
