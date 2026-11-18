namespace Storeasy.Application.Utils.Models;

/// <summary>
/// The deployment mode of the instance (configuration key <see cref="ConfigurationKey"/>, environment
/// <c>Storeasy__Mode</c>), bound once and registered as a singleton.
/// </summary>
public class DeploymentOptions
{
    /// <summary>
    /// Configuration key of <see cref="Mode"/>.
    /// </summary>
    public const string ConfigurationKey = "Storeasy:Mode";

    /// <summary>
    /// <see cref="DeploymentMode.SingleTeam"/> by default; <see cref="DeploymentMode.MultiTenant"/> for a hosted instance
    /// with many customers.
    /// </summary>
    public DeploymentMode Mode { get; set; } = DeploymentMode.SingleTeam;

    /// <summary>
    /// True in <see cref="DeploymentMode.SingleTeam"/>.
    /// </summary>
    public bool IsSingleTeam => Mode == DeploymentMode.SingleTeam;
}
