namespace Storeasy.Domain;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
public static class TableNames
{
    public const string MigrationHistory = "_MigrationHistory";
    public const string EfMigrationHistory = "_EFMigrationHistory";
    public const string Users = nameof(Users);
    public const string Roles = nameof(Roles);
    public const string UserRoleRelations = nameof(UserRoleRelations);
    public const string Permissions = nameof(Permissions);
    public const string RolePermissionRelations = nameof(RolePermissionRelations);
    public const string ActivityLogs = nameof(ActivityLogs);
    public const string UserSessions = nameof(UserSessions);
    public const string UserSessionHistories = nameof(UserSessionHistories);
    public const string Languages = nameof(Languages);
    public const string InternalNotifications = nameof(InternalNotifications);
    public const string IdempotencyKeys = nameof(IdempotencyKeys);
    public const string FeatureFlags = nameof(FeatureFlags);
    public const string Organizations = nameof(Organizations);
    public const string OrganizationMembers = nameof(OrganizationMembers);
    public const string OrganizationInvitations = nameof(OrganizationInvitations);
    public const string StoreConnections = nameof(StoreConnections);
    public const string StoreAuditLogs = nameof(StoreAuditLogs);
    public const string AccountTokens = nameof(AccountTokens);
    public const string StoreJobs = nameof(StoreJobs);
    public const string StoreJobSteps = nameof(StoreJobSteps);
    public const string Assets = nameof(Assets);
    public const string Apps = nameof(Apps);
    public const string StoreApps = nameof(StoreApps);
    public const string Builds = nameof(Builds);
    public const string SavedAnswers = nameof(SavedAnswers);
    public const string ChangePlans = nameof(ChangePlans);
    public const string AssistedSteps = nameof(AssistedSteps);
    public const string ProductGroups = nameof(ProductGroups);
    public const string StoreProducts = nameof(StoreProducts);
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member