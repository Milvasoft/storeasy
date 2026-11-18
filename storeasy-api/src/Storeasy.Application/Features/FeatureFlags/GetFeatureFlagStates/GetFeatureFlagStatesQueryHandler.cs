using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.FeatureFlagDtos;

namespace Storeasy.Application.Features.FeatureFlags.GetFeatureFlagStates;

/// <summary>
/// Handles <see cref="GetFeatureFlagStatesQuery"/>: one projection of the flag rows, no reason or auditing fields.
/// </summary>
/// <param name="featureFlagRepository"></param>
/// <param name="localizer"></param>
public class GetFeatureFlagStatesQueryHandler(IStoreasyRepositoryBase<FeatureFlag> featureFlagRepository, Milvasoft.Core.Abstractions.Localization.IMilvaLocalizer localizer) : IInterceptable, IQueryHandler<GetFeatureFlagStatesQuery, List<FeatureFlagStateDto>>
{
    private readonly IStoreasyRepositoryBase<FeatureFlag> _featureFlagRepository = featureFlagRepository;
    private readonly Milvasoft.Core.Abstractions.Localization.IMilvaLocalizer _localizer = localizer;

    /// <inheritdoc/>
    public async Task<Response<List<FeatureFlagStateDto>>> Handle(GetFeatureFlagStatesQuery request, CancellationToken cancellationToken)
    {
        var rows = (await _featureFlagRepository.GetAllAsync(f => FeatureFlagKeys.All.Contains(f.Key),
                                                             projection: f => new FeatureFlagStateDto { Key = f.Key, IsEnabled = f.IsEnabled },
                                                             cancellationToken: cancellationToken)).ToDictionary(f => f.Key);

        return Response<List<FeatureFlagStateDto>>.Success([.. FeatureFlagKeys.All.Select(key => new FeatureFlagStateDto
        {
            Key = key,
            IsEnabled = rows.GetValueOrDefault(key)?.IsEnabled ?? true,
            Description = _localizer[FeatureFlagKeys.DescriptionKey(key)],
        })]);
    }
}
