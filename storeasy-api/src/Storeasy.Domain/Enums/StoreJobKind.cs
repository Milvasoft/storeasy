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

    /// <summary>
    /// Renders a mockup template into every selected screenshot class and locale (studio export, phase 6). Numbered in the
    /// studio's own band.
    /// </summary>
    [Description("Mockup export")]
    MockupExport = 60,

    /// <summary>
    /// Reads the Microsoft Store products of one connection: MSIX apps, MSI/EXE products, submissions and their status,
    /// package flights, add-ons and installer packages.
    /// </summary>
    [Description("Sync Microsoft Store")]
    MsSync = 50,


    /// <summary>
    /// Commits a Microsoft Store submission (sends it to certification) and polls its status.
    /// </summary>
    [Description("Submit to Microsoft Store")]
    MsSubmit = 52,

    /// <summary>
    /// Downloads MSI/EXE installers and checks their Authenticode signature.
    /// </summary>
    [Description("Check Microsoft Store installers")]
    MsCheckInstaller = 53,
}
