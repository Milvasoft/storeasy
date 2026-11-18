using Milvasoft.Components.CQRS.Command;
using Milvasoft.Types.Structs;

namespace Storeasy.Application.Features.FeatureFlags.UpdateFeatureFlag;

/// <summary>
/// Data transfer object for feature flag update: switches a feature on or off, with an optional reason.
/// </summary>
public class UpdateFeatureFlagCommand : StoreasyBaseDto<int>, ICommand<int>
{
    /// <summary>
    /// Whether the feature should be on.
    /// </summary>
    public UpdateProperty<bool> IsEnabled { get; set; }

    /// <summary>
    /// Optional short reason for the switch.
    /// </summary>
    public UpdateProperty<string> Reason { get; set; }
}
