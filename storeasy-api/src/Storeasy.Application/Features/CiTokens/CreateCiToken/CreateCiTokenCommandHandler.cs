using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.IntakeDtos;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Intake;
using Storeasy.Domain.Tokens;

namespace Storeasy.Application.Features.CiTokens.CreateCiToken;

/// <summary>
/// Handles <see cref="CreateCiTokenCommand"/>. Not logged: the response carries the token value.
/// </summary>
/// <param name="TokenRepository"></param>
/// <param name="AppRepository"></param>
/// <param name="CurrentUser"></param>
/// <param name="AuditTrail"></param>
/// <param name="TimeProvider"></param>
public record CreateCiTokenCommandHandler(IStoreasyRepositoryBase<CiToken> TokenRepository,
                                          IStoreasyRepositoryBase<App> AppRepository,
                                          ICurrentUser CurrentUser,
                                          IAuditTrail AuditTrail,
                                          TimeProvider TimeProvider) : IInterceptable, ICommandHandler<CreateCiTokenCommand, CiTokenCreatedDto>
{
    private readonly IStoreasyRepositoryBase<CiToken> _tokenRepository = TokenRepository;
    private readonly IStoreasyRepositoryBase<App> _appRepository = AppRepository;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly IAuditTrail _auditTrail = AuditTrail;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<CiTokenCreatedDto>> Handle(CreateCiTokenCommand request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var name = request.Name.Trim();
        var appIds = request.AllApps ? [] : request.AppIds.Distinct().ToList();

        if (appIds.Count > 0)
        {
            var found = await _appRepository.GetAllAsync(a => appIds.Contains(a.Id) && a.OrganizationId == request.OrganizationId && !a.IsDeleted,
                                                         projection: a => new App { Id = a.Id },
                                                         cancellationToken: cancellationToken);

            if (found.Count() != appIds.Count)
                return Response<CiTokenCreatedDto>.Error(null, MessageKey.AppNotFound);
        }

        var existing = await _tokenRepository.GetFirstOrDefaultAsync(t => t.OrganizationId == request.OrganizationId && t.Name == name && t.RevokeDate == null && t.ExpiryDate > now,
                                                                     projection: t => new CiToken { Id = t.Id },
                                                                     cancellationToken: cancellationToken);

        if (existing != null)
            return Response<CiTokenCreatedDto>.Error(null, MessageKey.TokenNameExists);

        var value = SecureTokens.Create(TokenPrefixes.CiToken);

        var token = new CiToken
        {
            OrganizationId = request.OrganizationId,
            Name = name,
            TokenHash = SecureTokens.Hash(value),
            Prefix = TokenPrefixes.DisplayPrefixOf(value),
            AllApps = request.AllApps,
            AppIds = appIds,
            Scopes = [CiToken.ArtifactsWriteScope],
            ExpiryDate = now.AddDays(request.ExpiryDays),
            CreatedByUserId = _currentUser.Id,
            CreatedByUserName = _currentUser.UserName,
        };

        await _tokenRepository.AddAsync(token, cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.CiTokenCreated,
                                     token.Id.ToString(),
                                     $"CI token {token.Prefix} \"{name}\" created.",
                                     details: new { token.Id, token.Prefix, token.Name, token.AllApps, token.AppIds, token.ExpiryDate },
                                     cancellationToken: cancellationToken);

        return Response<CiTokenCreatedDto>.Success(new CiTokenCreatedDto { Id = token.Id, Token = value, Prefix = token.Prefix, ExpiryDate = token.ExpiryDate });
    }
}
