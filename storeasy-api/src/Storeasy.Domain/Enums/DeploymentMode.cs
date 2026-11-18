using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// How an instance is used (<c>Storeasy:Mode</c>).
/// </summary>
public enum DeploymentMode : byte
{
    /// <summary>
    /// One team installs and uses its own instance (the default): the database is prepared on startup, the first visitor
    /// with the setup code creates the administrator and the team, there is exactly one organization and people join by
    /// invitation.
    /// </summary>
    [Description("Single team")]
    SingleTeam = 1,

    /// <summary>
    /// A hosted instance for many customers: open registration (by <c>Storeasy:Registration:Mode</c>), any number of
    /// organizations and a platform administrator created by <c>developer/database/init</c>.
    /// </summary>
    [Description("Multi tenant")]
    MultiTenant = 2,
}
