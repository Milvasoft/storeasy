using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.TokenDtos;
using Storeasy.Domain.Intake;
using Storeasy.Domain.Organizations;
using Storeasy.Domain.Tokens;

namespace Storeasy.Application.Features.Tokens.GetCurrentToken;

/// <summary>
/// Handles <see cref="GetCurrentTokenQuery"/>.
/// </summary>
/// <param name="CurrentToken"></param>
/// <param name="CurrentUser"></param>
/// <param name="OrganizationRepository"></param>
/// <param name="PersonalTokenRepository"></param>
/// <param name="CiTokenRepository"></param>
/// <param name="UploadTokenRepository"></param>
public record GetCurrentTokenQueryHandler(ICurrentToken CurrentToken,
                                          ICurrentUser CurrentUser,
                                          IStoreasyRepositoryBase<Organization> OrganizationRepository,
                                          IStoreasyRepositoryBase<PersonalAccessToken> PersonalTokenRepository,
                                          IStoreasyRepositoryBase<CiToken> CiTokenRepository,
                                          IStoreasyRepositoryBase<UploadToken> UploadTokenRepository) : IInterceptable, IQueryHandler<GetCurrentTokenQuery, CurrentTokenDto>
{
    private readonly ICurrentToken _currentToken = CurrentToken;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly IStoreasyRepositoryBase<Organization> _organizationRepository = OrganizationRepository;
    private readonly IStoreasyRepositoryBase<PersonalAccessToken> _personalTokenRepository = PersonalTokenRepository;
    private readonly IStoreasyRepositoryBase<CiToken> _ciTokenRepository = CiTokenRepository;
    private readonly IStoreasyRepositoryBase<UploadToken> _uploadTokenRepository = UploadTokenRepository;

    /// <inheritdoc/>
    public async Task<Response<CurrentTokenDto>> Handle(GetCurrentTokenQuery request, CancellationToken cancellationToken)
    {
        if (_currentToken.Kind is not { } kind || _currentToken.TokenId is not { } tokenId || _currentToken.OrganizationId is not { } organizationId)
            return Response<CurrentTokenDto>.Error(null, MessageKey.PersonalAccessTokenNotFound);

        var organization = await _organizationRepository.GetFirstOrDefaultAsync(o => o.Id == organizationId,
                                                                                projection: o => new Organization { Id = o.Id, Name = o.Name },
                                                                                cancellationToken: cancellationToken);

        var dto = new CurrentTokenDto
        {
            Kind = kind,
            Name = _currentToken.Name,
            Prefix = _currentToken.Prefix,
            OrganizationId = organizationId,
            OrganizationName = organization?.Name,
            ReadOnly = _currentToken.ReadOnly,
            AllApps = kind == TokenKind.PersonalAccessToken || _currentToken.AllApps,
            AppIds = [.. _currentToken.AppIds],
        };

        switch (kind)
        {
            case TokenKind.PersonalAccessToken:
                dto.UserName = _currentUser.UserName;
                dto.ExpiryDate = await _personalTokenRepository.GetFirstOrDefaultAsync(t => t.Id == tokenId, projection: t => t.ExpiryDate, cancellationToken: cancellationToken);
                break;
            case TokenKind.CiToken:
                dto.Scopes = [CiToken.ArtifactsWriteScope];
                dto.ExpiryDate = await _ciTokenRepository.GetFirstOrDefaultAsync(t => t.Id == tokenId, projection: t => t.ExpiryDate, cancellationToken: cancellationToken);
                break;
            default:
                dto.Scopes = [CiToken.ArtifactsWriteScope];
                dto.ExpiryDate = await _uploadTokenRepository.GetFirstOrDefaultAsync(t => t.Id == tokenId, projection: t => t.ExpiryDate, cancellationToken: cancellationToken);
                break;
        }

        return Response<CurrentTokenDto>.Success(dto);
    }
}
