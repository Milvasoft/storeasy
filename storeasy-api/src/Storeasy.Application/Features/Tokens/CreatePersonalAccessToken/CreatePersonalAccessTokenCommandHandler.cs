using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.TokenDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Tokens;

namespace Storeasy.Application.Features.Tokens.CreatePersonalAccessToken;

/// <summary>
/// Handles <see cref="CreatePersonalAccessTokenCommand"/>. Not logged: the response carries the token value.
/// </summary>
/// <param name="TokenRepository"></param>
/// <param name="OrganizationAccess"></param>
/// <param name="CurrentUser"></param>
/// <param name="AuditTrail"></param>
/// <param name="TimeProvider"></param>
public record CreatePersonalAccessTokenCommandHandler(IStoreasyRepositoryBase<PersonalAccessToken> TokenRepository,
                                                      IOrganizationAccess OrganizationAccess,
                                                      ICurrentUser CurrentUser,
                                                      IAuditTrail AuditTrail,
                                                      TimeProvider TimeProvider) : IInterceptable, ICommandHandler<CreatePersonalAccessTokenCommand, PersonalAccessTokenCreatedDto>
{
    private readonly IStoreasyRepositoryBase<PersonalAccessToken> _tokenRepository = TokenRepository;
    private readonly IOrganizationAccess _organizationAccess = OrganizationAccess;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<PersonalAccessTokenCreatedDto>> Handle(CreatePersonalAccessTokenCommand request, CancellationToken cancellationToken)
    {
        if (!request.ReadOnly && _organizationAccess.Current?.CanWrite != true)
            return Response<PersonalAccessTokenCreatedDto>.Error(null, MessageKey.OrganizationForbidden);

        var userId = _currentUser.GetRequiredId();
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var name = request.Name.Trim();

        var existing = await _tokenRepository.GetFirstOrDefaultAsync(t => t.UserId == userId && t.OrganizationId == request.OrganizationId && t.Name == name && t.RevokeDate == null && t.ExpiryDate > now,
                                                                     projection: t => new PersonalAccessToken { Id = t.Id },
                                                                     cancellationToken: cancellationToken);

        if (existing != null)
            return Response<PersonalAccessTokenCreatedDto>.Error(null, MessageKey.TokenNameExists);

        var value = SecureTokens.Create(TokenPrefixes.PersonalAccessToken);

        var token = new PersonalAccessToken
        {
            UserId = userId,
            OrganizationId = request.OrganizationId,
            Name = name,
            TokenHash = SecureTokens.Hash(value),
            Prefix = TokenPrefixes.DisplayPrefixOf(value),
            ReadOnly = request.ReadOnly,
            ExpiryDate = now.AddDays(request.ExpiryDays),
        };

        await _tokenRepository.AddAsync(token, cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.TokenCreated,
                                     token.Id.ToString(),
                                     $"Personal access token {token.Prefix} \"{name}\" created ({(request.ReadOnly ? "read only" : "read and write")}).",
                                     details: new { token.Id, token.Prefix, token.Name, token.ReadOnly, token.ExpiryDate },
                                     cancellationToken: cancellationToken);

        return Response<PersonalAccessTokenCreatedDto>.Success(new PersonalAccessTokenCreatedDto { Id = token.Id, Token = value, Prefix = token.Prefix, ExpiryDate = token.ExpiryDate });
    }
}
