using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.TokenDtos;
using Storeasy.Domain.Tokens;

namespace Storeasy.Application.Features.Tokens.GetPersonalAccessTokenList;

/// <summary>
/// Handles <see cref="GetPersonalAccessTokenListQuery"/>.
/// </summary>
/// <param name="TokenRepository"></param>
/// <param name="CurrentUser"></param>
/// <param name="TimeProvider"></param>
public record GetPersonalAccessTokenListQueryHandler(IStoreasyRepositoryBase<PersonalAccessToken> TokenRepository,
                                                     ICurrentUser CurrentUser,
                                                     TimeProvider TimeProvider) : IInterceptable, IQueryHandler<GetPersonalAccessTokenListQuery, List<PersonalAccessTokenDto>>
{
    private readonly IStoreasyRepositoryBase<PersonalAccessToken> _tokenRepository = TokenRepository;
    private readonly ICurrentUser _currentUser = CurrentUser;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<List<PersonalAccessTokenDto>>> Handle(GetPersonalAccessTokenListQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredId();
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var listedSince = now.AddDays(-30);

        var tokens = await _tokenRepository.GetAllAsync(t => t.UserId == userId && t.RevokeDate == null && t.ExpiryDate > listedSince && !t.Organization.IsDeleted,
                                                        projection: t => new PersonalAccessTokenDto
                                                        {
                                                            Id = t.Id,
                                                            Name = t.Name,
                                                            OrganizationId = t.OrganizationId,
                                                            OrganizationName = t.Organization.Name,
                                                            ReadOnly = t.ReadOnly,
                                                            Prefix = t.Prefix,
                                                            ExpiryDate = t.ExpiryDate,
                                                            LastUseDate = t.LastUseDate,
                                                            LastClientName = t.LastClientName,
                                                            CreationDate = t.CreationDate,
                                                            IsExpired = t.ExpiryDate <= now,
                                                        },
                                                        cancellationToken: cancellationToken);

        return Response<List<PersonalAccessTokenDto>>.Success([.. tokens.OrderByDescending(t => t.CreationDate)]);
    }
}
