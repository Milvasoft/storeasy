using Milvasoft.Components.CQRS.Query;
using Storeasy.Application.Dtos.FeatureFlagDtos;

namespace Storeasy.Application.Features.FeatureFlags.GetFeatureFlagStates;

/// <summary>
/// Reads whether each feature of <see cref="FeatureFlagKeys.All"/> is on (read only, every signed in user). A key without
/// a row counts as on, like <see cref="IFeatureFlagService"/>.
/// </summary>
public record GetFeatureFlagStatesQuery : IQuery<List<FeatureFlagStateDto>>
{
}
