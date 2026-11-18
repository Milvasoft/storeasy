using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.Request;
using Storeasy.Application.Dtos.FeatureFlagDtos;

namespace Storeasy.Application.Features.FeatureFlags.GetFeatureFlagList;

/// <summary>
/// Data transfer object for feature flag list.
/// </summary>
public record GetFeatureFlagListQuery : ListRequest, IListRequestQuery<FeatureFlagListDto>
{
}
