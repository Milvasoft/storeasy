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
}
