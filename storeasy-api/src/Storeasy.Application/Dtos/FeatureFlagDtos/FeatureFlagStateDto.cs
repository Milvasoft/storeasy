namespace Storeasy.Application.Dtos.FeatureFlagDtos;

/// <summary>
/// Whether one feature is on, as every signed in user may read it (the web hides what is off).
/// </summary>
public class FeatureFlagStateDto
{
    /// <summary>
    /// Feature key (<see cref="FeatureFlagKeys"/>).
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Whether the feature is on.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// What the feature is (localized).
    /// </summary>
    public string Description { get; set; }
}
