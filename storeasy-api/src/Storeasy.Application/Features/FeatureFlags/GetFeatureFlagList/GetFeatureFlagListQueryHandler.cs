using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.FeatureFlagDtos;

namespace Storeasy.Application.Features.FeatureFlags.GetFeatureFlagList;

/// <summary>
/// Handles the feature flag list operation. Every key of <see cref="FeatureFlagKeys.All"/> has a row (seeded on startup).
/// </summary>
/// <param name="featureFlagRepository"></param>
/// <param name="localizer"></param>
public class GetFeatureFlagListQueryHandler(IStoreasyRepositoryBase<FeatureFlag> featureFlagRepository, Milvasoft.Core.Abstractions.Localization.IMilvaLocalizer localizer) : IInterceptable, IListQueryHandler<GetFeatureFlagListQuery, FeatureFlagListDto>
{
    private readonly IStoreasyRepositoryBase<FeatureFlag> _featureFlagRepository = featureFlagRepository;
    private readonly Milvasoft.Core.Abstractions.Localization.IMilvaLocalizer _localizer = localizer;

    /// <inheritdoc/>
    public async Task<ListResponse<FeatureFlagListDto>> Handle(GetFeatureFlagListQuery request, CancellationToken cancellationToken)
    {
        request.PageNumber ??= 1;
        request.RowCount = request.RowCount.ClampRowCount(20);

        var response = await _featureFlagRepository.GetAllAsync(request, projection: FeatureFlagListDto.Projection, cancellationToken: cancellationToken);

        response.Data?.ForEach(flag => flag.Description = _localizer[FeatureFlagKeys.DescriptionKey(flag.Key)]);

        return response;
    }
}
