using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore.Utils;
using Storeasy.Domain.Tokens;

namespace Storeasy.Application.Features.Tokens.RevokePersonalAccessToken;

/// <summary>
/// Handles <see cref="RevokePersonalAccessTokenCommand"/>.
/// </summary>
/// <param name="TokenRepository"></param>
/// <param name="CurrentUser"></param>
/// <param name="AuditTrail"></param>
/// <param name="TimeProvider"></param>
public record RevokePersonalAccessTokenCommandHandler(IStoreasyRepositoryBase<PersonalAccessToken> TokenRepository,
                                                      ICurrentUser CurrentUser,
                                                      IAuditTrail AuditTrail,
                                                      TimeProvider TimeProvider) : IInterceptable, ICommandHandler<RevokePersonalAccessTokenCommand, int>
{
    private readonly IStoreasyRepositoryBase<PersonalAccessToken> _tokenRepository = TokenRepository;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(RevokePersonalAccessTokenCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredId();

        var token = await _tokenRepository.GetFirstOrDefaultAsync(t => t.Id == request.TokenId && t.UserId == userId && t.RevokeDate == null,
                                                                  projection: t => new PersonalAccessToken { Id = t.Id, OrganizationId = t.OrganizationId, Prefix = t.Prefix, Name = t.Name },
                                                                  cancellationToken: cancellationToken);

        if (token == null)
            return Response<int>.Error(0, MessageKey.PersonalAccessTokenNotFound);

        var revoked = await _tokenRepository.ExecuteUpdateAsync(t => t.Id == token.Id && t.RevokeDate == null,
                                                                new SetPropertyBuilder<PersonalAccessToken>().SetPropertyValue(t => t.RevokeDate, (DateTime?)_timeProvider.GetUtcNow().UtcDateTime),
                                                                cancellationToken);

        if (revoked == 0)
            return Response<int>.Error(0, MessageKey.PersonalAccessTokenNotFound);

        await _auditTrail.WriteAsync(token.OrganizationId,
                                     AuditAction.TokenRevoked,
                                     token.Id.ToString(),
                                     $"Personal access token {token.Prefix} \"{token.Name}\" revoked.",
                                     details: new { token.Id, token.Prefix },
                                     cancellationToken: cancellationToken);

        return Response<int>.Success(token.Id);
    }
}
