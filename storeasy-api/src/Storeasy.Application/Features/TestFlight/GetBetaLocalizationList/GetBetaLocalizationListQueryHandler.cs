using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.TestFlightDtos;
using Storeasy.Domain.Apps;
using Storeasy.Domain.TestFlight;

namespace Storeasy.Application.Features.TestFlight.GetBetaLocalizationList;

/// <summary>
/// Handles <see cref="GetBetaLocalizationListQuery"/>.
/// </summary>
/// <param name="StoreAppRepository"></param>
/// <param name="LocalizationRepository"></param>
public record GetBetaLocalizationListQueryHandler(IStoreasyRepositoryBase<StoreApp> StoreAppRepository,
                                                  IStoreasyRepositoryBase<BetaAppLocalization> LocalizationRepository) : IInterceptable, IQueryHandler<GetBetaLocalizationListQuery, List<BetaLocalizationDto>>
{
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = StoreAppRepository;
    private readonly IStoreasyRepositoryBase<BetaAppLocalization> _localizationRepository = LocalizationRepository;

    /// <inheritdoc/>
    public async Task<Response<List<BetaLocalizationDto>>> Handle(GetBetaLocalizationListQuery request, CancellationToken cancellationToken)
    {
        var storeApp = await _storeAppRepository.FindAppleRecordAsync(request.OrganizationId, request.StoreAppId, cancellationToken);

        if (storeApp == null)
            return Response<List<BetaLocalizationDto>>.Error(null, MessageKey.StoreAppNotFound);

        var localizations = await _localizationRepository.GetAllAsync(l => l.StoreAppId == storeApp.Id,
                                                                      projection: BetaLocalizationDto.Projection,
                                                                      cancellationToken: cancellationToken);

        return Response<List<BetaLocalizationDto>>.Success([.. localizations.OrderBy(l => l.Locale)]);
    }
}
