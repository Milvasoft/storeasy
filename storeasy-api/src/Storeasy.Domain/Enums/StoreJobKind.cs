using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// What a store job does. Each kind has one handler in the store engine. Values are explicit and never reused.
/// </summary>
public enum StoreJobKind : short
{
    /// <summary>
    /// Pulls the apps and builds of one store connection and links them to storeasy apps.
    /// </summary>
    [Description("Sync apps and builds")]
    SyncApps = 1,

    /// <summary>
    /// Applies an approved change plan to a store.
    /// </summary>
    [Description("Apply a change plan")]
    ApplyChangePlan = 2,
}
