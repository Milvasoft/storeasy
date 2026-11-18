using System.ComponentModel;

namespace Storeasy.Domain.Enums;

/// <summary>
/// User activity types. The numeric value is stored in <c>ActivityLogs</c>, so values are explicit and never reused.
/// </summary>
public enum UserActivity : byte
{
    /// <summary>
    /// User added.
    /// </summary>
    [Description("User added")]
    CreateUser = 0,

    /// <summary>
    /// User updated.
    /// </summary>
    [Description("User updated")]
    UpdateUser = 1,

    /// <summary>
    /// User deleted.
    /// </summary>
    [Description("User deleted")]
    DeleteUser = 2,

    /// <summary>
    /// Role added.
    /// </summary>
    [Description("Role added")]
    CreateRole = 3,

    /// <summary>
    /// Role updated.
    /// </summary>
    [Description("Role updated")]
    UpdateRole = 4,

    /// <summary>
    /// Role deleted.
    /// </summary>
    [Description("Role deleted")]
    DeleteRole = 5,

    /// <summary>
    /// Languages updated.
    /// </summary>
    [Description("Languages updated")]
    UpdateLanguages = 15,

    /// <summary>
    /// Feature flag updated.
    /// </summary>
    [Description("Feature flag updated")]
    UpdateFeatureFlag = 16
}