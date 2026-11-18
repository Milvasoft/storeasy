using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// The TestFlight write a <see cref="StoreJobKind.UpdateTestFlight"/> job carries. Values are explicit and never reused.
/// </summary>
public enum TestFlightOperation : byte
{
    /// <summary>Create a beta group.</summary>
    [Description("Create group")]
    CreateGroup = 1,

    /// <summary>Rename a group or change its public link.</summary>
    [Description("Update group")]
    UpdateGroup = 2,

    /// <summary>Add or remove testers of a group.</summary>
    [Description("Update testers")]
    UpdateTesters = 3,

    /// <summary>Add or remove builds of a group.</summary>
    [Description("Update builds")]
    UpdateBuilds = 4,

    /// <summary>Write the "what to test" text of a build.</summary>
    [Description("What to test")]
    WhatToTest = 5,

    /// <summary>Write the beta app description and feedback e-mail of a language.</summary>
    [Description("Beta localization")]
    Localization = 6,

    /// <summary>Submit a build to beta app review.</summary>
    [Description("Submit for beta review")]
    SubmitBetaReview = 7,
}
