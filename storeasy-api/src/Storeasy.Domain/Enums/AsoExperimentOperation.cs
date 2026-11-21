using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// A store write of a store listing experiment, always made as a change plan.
/// </summary>
public enum AsoExperimentOperation : byte
{
    /// <summary>
    /// Creates or updates the experiment (or custom product page) in the store with its treatments, localizations,
    /// screenshots and previews.
    /// </summary>
    [Description("Write to the store")]
    Push = 1,

    /// <summary>
    /// Submits it for App Review, alone or together with the app version.
    /// </summary>
    [Description("Submit for review")]
    Submit = 2,

    /// <summary>
    /// Starts an approved experiment.
    /// </summary>
    [Description("Start")]
    Start = 3,

    /// <summary>
    /// Stops a running experiment.
    /// </summary>
    [Description("Stop")]
    Stop = 4,

    /// <summary>
    /// Writes the winning treatment's screenshots and previews to the editable app version.
    /// </summary>
    [Description("Apply winner")]
    ApplyWinner = 5,
}
