using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.IntakeDtos;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Intake;

namespace Storeasy.Application.Features.CiTokens.GetCiTokenList;

/// <summary>
/// Handles <see cref="GetCiTokenListQuery"/>.
/// </summary>
/// <param name="TokenRepository"></param>
/// <param name="AppRepository"></param>
/// <param name="TimeProvider"></param>
public record GetCiTokenListQueryHandler(IStoreasyRepositoryBase<CiToken> TokenRepository,
                                         IStoreasyRepositoryBase<App> AppRepository,
                                         TimeProvider TimeProvider) : IInterceptable, IQueryHandler<GetCiTokenListQuery, List<CiTokenDto>>
{
    private readonly IStoreasyRepositoryBase<CiToken> _tokenRepository = TokenRepository;
    private readonly IStoreasyRepositoryBase<App> _appRepository = AppRepository;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<List<CiTokenDto>>> Handle(GetCiTokenListQuery request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var tokens = await _tokenRepository.GetAllAsync(t => t.OrganizationId == request.OrganizationId && t.RevokeDate == null,
                                                        projection: t => new CiTokenDto
                                                        {
                                                            Id = t.Id,
                                                            Name = t.Name,
                                                            AppIds = t.AppIds,
                                                            AllApps = t.AllApps,
                                                            Scopes = t.Scopes,
                                                            Prefix = t.Prefix,
                                                            ExpiryDate = t.ExpiryDate,
                                                            IsExpired = t.ExpiryDate <= now,
                                                            LastUseDate = t.LastUseDate,
                                                            LastUseSource = t.LastUseSource,
                                                            CreatorUserName = t.CreatedByUserName,
                                                            CreationDate = t.CreationDate,
                                                        },
                                                        cancellationToken: cancellationToken);

        var names = (await _appRepository.GetAllAsync(a => a.OrganizationId == request.OrganizationId,
                                                      projection: a => new App { Id = a.Id, Name = a.Name },
                                                      cancellationToken: cancellationToken)).ToDictionary(a => a.Id, a => a.Name);

        foreach (var token in tokens)
        {
            token.AppIds ??= [];
            token.AppNames = [.. token.AppIds.Select(id => names.GetValueOrDefault(id)).Where(n => n != null)];
        }

        return Response<List<CiTokenDto>>.Success([.. tokens.OrderByDescending(t => t.CreationDate)]);
    }
}
