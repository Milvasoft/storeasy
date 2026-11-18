namespace Storeasy.Domain.Enums;

/// <summary>
/// Notication related entity types.
/// </summary>
public enum NotificationEntity : byte
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    None = 0,
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    /// <summary>A storeasy app.</summary>
    App = 40,

    /// <summary>A store job.</summary>
    StoreJob = 41,

    /// <summary>A rejection case.</summary>
    RejectionCase = 42,

    /// <summary>A store review.</summary>
    Review = 43,

    /// <summary>An organization invitation.</summary>
    Invitation = 44,

    /// <summary>A store connection.</summary>
    StoreConnection = 45,

    /// <summary>A package delivery session.</summary>
    ArtifactSession = 46,

    /// <summary>A build.</summary>
    Build = 47,

    /// <summary>A rules catalog proposal.</summary>
    RuleProposal = 48,

    /// <summary>An inbound webhook event.</summary>
    WebhookEvent = 49,

    /// <summary>A rules catalog rule.</summary>
    Rule = 50,

    /// <summary>An App Store version state.</summary>
    AppVersionState = 51,
}
