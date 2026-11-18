using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// Kinds of organization audit entries. Values are explicit and never reused.
/// </summary>
public enum AuditAction : short
{
    /// <summary>Organization created.</summary>
    [Description("Organization created")]
    OrganizationCreated = 1,

    /// <summary>Organization renamed.</summary>
    [Description("Organization updated")]
    OrganizationUpdated = 2,

    /// <summary>Invitation sent.</summary>
    [Description("Member invited")]
    MemberInvited = 10,

    /// <summary>Invitation revoked.</summary>
    [Description("Invitation revoked")]
    InvitationRevoked = 11,

    /// <summary>Invitation accepted.</summary>
    [Description("Member joined")]
    MemberJoined = 12,

    /// <summary>Member role changed.</summary>
    [Description("Member role changed")]
    MemberRoleChanged = 13,

    /// <summary>Member removed or left.</summary>
    [Description("Member removed")]
    MemberRemoved = 14,

    /// <summary>Store connection added.</summary>
    [Description("Store connection added")]
    ConnectionAdded = 20,

    /// <summary>Store connection secret replaced or renamed.</summary>
    [Description("Store connection updated")]
    ConnectionUpdated = 21,

    /// <summary>Store connection removed.</summary>
    [Description("Store connection removed")]
    ConnectionRemoved = 22,

    /// <summary>Store connection verified against the store.</summary>
    [Description("Store connection verified")]
    ConnectionVerified = 23,

    /// <summary>Store job cancelled.</summary>
    [Description("Store job cancelled")]
    JobCancelled = 30,

    /// <summary>A store record was added by hand (a Play app by package name).</summary>
    [Description("App added")]
    AppAdded = 40,

    /// <summary>A store record was moved to another storeasy app.</summary>
    [Description("Store record linked")]
    StoreAppLinked = 41,

    /// <summary>A store record was split off into its own storeasy app.</summary>
    [Description("Store record unlinked")]
    StoreAppUnlinked = 42,

    /// <summary>A Microsoft Store change was queued (listings, images, packages, pricing, flights, add-ons, rollout).</summary>
    [Description("Microsoft Store change queued")]
    MsChangeQueued = 150,

    /// <summary>A Microsoft Store change was written to Partner Center.</summary>
    [Description("Microsoft Store change applied")]
    MsChangeApplied = 151,

    /// <summary>A Microsoft Store submission was committed (sent to certification) or an MSI/EXE product submitted.</summary>
    [Description("Microsoft Store submission sent")]
    MsSubmitted = 152,

    /// <summary>An MSI/EXE product was added by its product id.</summary>
    [Description("Microsoft Store product added")]
    MsProductAdded = 153,

    /// <summary>The client secret of a Microsoft Entra connection was replaced.</summary>
    [Description("Microsoft connection secret rotated")]
    MsConnectionSecretRotated = 154,

    /// <summary>A package flight group name was recorded.</summary>
    [Description("Microsoft flight group saved")]
    MsFlightGroupSaved = 155,
}
