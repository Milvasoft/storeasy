using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Storeasy.Domain.JsonModels;

#nullable disable

namespace Storeasy.Api.Migrations;

/// <inheritdoc />
public partial class _20261118082902_InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "_MigrationHistory",
            columns: table => new
            {
                MigrationId = table.Column<string>(type: "text", nullable: false),
                MigrationCompleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK__MigrationHistory", x => x.MigrationId);
            });

        migrationBuilder.CreateTable(
            name: "ActivityLogs",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Activity = table.Column<byte>(type: "smallint", nullable: false),
                ActivityDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                RelatedId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                RelatedData = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ActivityLogs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "FeatureFlags",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true),
                DeletionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeleterUserName = table.Column<string>(type: "text", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FeatureFlags", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "IdempotencyKeys",
            columns: table => new
            {
                UserId = table.Column<int>(type: "integer", nullable: false),
                Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Endpoint = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                RequestHash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: true),
                ResponseData = table.Column<string>(type: "text", nullable: true),
                CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IdempotencyKeys", x => new { x.UserId, x.Key });
            });

        migrationBuilder.CreateTable(
            name: "InternalNotifications",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                RecipientUserName = table.Column<string>(type: "text", nullable: true),
                RecipientUserId = table.Column<int>(type: "integer", nullable: false),
                Type = table.Column<byte>(type: "smallint", nullable: false),
                SeenDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Text = table.Column<string>(type: "text", nullable: true),
                Data = table.Column<string>(type: "text", nullable: true),
                RelatedEntityType = table.Column<byte>(type: "smallint", nullable: false),
                RelatedEntityId = table.Column<string>(type: "text", nullable: true),
                ActionLink = table.Column<string>(type: "text", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InternalNotifications", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Languages",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>(type: "text", nullable: true),
                Code = table.Column<string>(type: "text", nullable: true),
                Supported = table.Column<bool>(type: "boolean", nullable: false),
                IsDefault = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Languages", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Organizations",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Slug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true),
                DeletionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeleterUserName = table.Column<string>(type: "text", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Organizations", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Permissions",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Description = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                NormalizedName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                PermissionGroup = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                PermissionGroupDescription = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Permissions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Roles",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true),
                DeletionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeleterUserName = table.Column<string>(type: "text", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Roles", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "StoreAuditLogs",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                UserId = table.Column<int>(type: "integer", nullable: true),
                UserName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Channel = table.Column<byte>(type: "smallint", nullable: false),
                Action = table.Column<short>(type: "smallint", nullable: false),
                Store = table.Column<byte>(type: "smallint", nullable: true),
                TargetId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                Details = table.Column<string>(type: "jsonb", nullable: true),
                Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StoreAuditLogs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Surname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                UserType = table.Column<byte>(type: "smallint", nullable: false),
                AllowedNotifications = table.Column<string>(type: "jsonb", nullable: true),
                SessionStamp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true),
                DeleterUserName = table.Column<string>(type: "text", nullable: true),
                DeletionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                UserName = table.Column<string>(type: "text", nullable: true),
                NormalizedUserName = table.Column<string>(type: "text", nullable: true),
                Email = table.Column<string>(type: "text", nullable: true),
                NormalizedEmail = table.Column<string>(type: "text", nullable: true),
                EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                PasswordHash = table.Column<string>(type: "text", nullable: true),
                SecurityStamp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                PhoneNumber = table.Column<string>(type: "text", nullable: true),
                PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Users", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "UserSessionHistories",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserName = table.Column<string>(type: "text", nullable: true),
                AccessToken = table.Column<string>(type: "text", nullable: true),
                RefreshToken = table.Column<string>(type: "text", nullable: true),
                ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                DeviceId = table.Column<string>(type: "text", nullable: true),
                UserId = table.Column<int>(type: "integer", nullable: false),
                IpAddress = table.Column<string>(type: "text", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserSessionHistories", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "OrganizationInvitations",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                Role = table.Column<byte>(type: "smallint", nullable: false),
                TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                AcceptDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                RevokeDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                InviterUserId = table.Column<int>(type: "integer", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationInvitations", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationInvitations_Organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "StoreConnections",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                Store = table.Column<byte>(type: "smallint", nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Fingerprint = table.Column<ConnectionFingerprint>(type: "jsonb", nullable: true),
                EncryptedSecret = table.Column<byte[]>(type: "bytea", nullable: true),
                EncryptedDataKey = table.Column<byte[]>(type: "bytea", nullable: true),
                KeyVersion = table.Column<int>(type: "integer", nullable: false),
                Status = table.Column<byte>(type: "smallint", nullable: false),
                LastVerifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastVerifyError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true),
                DeletionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeleterUserName = table.Column<string>(type: "text", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StoreConnections", x => x.Id);
                table.ForeignKey(
                    name: "FK_StoreConnections_Organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "RolePermissionRelations",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                RoleId = table.Column<int>(type: "integer", nullable: false),
                PermissionId = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RolePermissionRelations", x => x.Id);
                table.ForeignKey(
                    name: "FK_RolePermissionRelations_Permissions_PermissionId",
                    column: x => x.PermissionId,
                    principalTable: "Permissions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_RolePermissionRelations_Roles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "Roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "OrganizationMembers",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                UserId = table.Column<int>(type: "integer", nullable: false),
                Role = table.Column<byte>(type: "smallint", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationMembers", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationMembers_Organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_OrganizationMembers_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "UserRoleRelations",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<int>(type: "integer", nullable: false),
                RoleId = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserRoleRelations", x => x.Id);
                table.ForeignKey(
                    name: "FK_UserRoleRelations_Roles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "Roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_UserRoleRelations_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "UserSessions",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserName = table.Column<string>(type: "text", nullable: true),
                AccessToken = table.Column<string>(type: "text", nullable: true),
                RefreshToken = table.Column<string>(type: "text", nullable: true),
                ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                DeviceId = table.Column<string>(type: "text", nullable: true),
                IpAddress = table.Column<string>(type: "text", nullable: true),
                UserId = table.Column<int>(type: "integer", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserSessions", x => x.Id);
                table.ForeignKey(
                    name: "FK_UserSessions_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ActivityLogs_ActivityDate",
            table: "ActivityLogs",
            column: "ActivityDate",
            descending: new bool[0]);

        migrationBuilder.CreateIndex(
            name: "IX_ActivityLogs_UserName_ActivityDate",
            table: "ActivityLogs",
            columns: new[] { "UserName", "ActivityDate" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "IX_FeatureFlags_Key",
            table: "FeatureFlags",
            column: "Key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_IdempotencyKeys_CreationDate",
            table: "IdempotencyKeys",
            column: "CreationDate");

        migrationBuilder.CreateIndex(
            name: "IX_InternalNotifications_RecipientUserName",
            table: "InternalNotifications",
            column: "RecipientUserName");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationInvitations_OrganizationId_NormalizedEmail",
            table: "OrganizationInvitations",
            columns: new[] { "OrganizationId", "NormalizedEmail" });

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationInvitations_TokenHash",
            table: "OrganizationInvitations",
            column: "TokenHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationMembers_OrganizationId_UserId",
            table: "OrganizationMembers",
            columns: new[] { "OrganizationId", "UserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationMembers_UserId",
            table: "OrganizationMembers",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_Organizations_Slug",
            table: "Organizations",
            column: "Slug",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Permissions_PermissionGroup_Name",
            table: "Permissions",
            columns: new[] { "PermissionGroup", "Name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RolePermissionRelations_PermissionId",
            table: "RolePermissionRelations",
            column: "PermissionId");

        migrationBuilder.CreateIndex(
            name: "IX_RolePermissionRelations_RoleId",
            table: "RolePermissionRelations",
            column: "RoleId");

        migrationBuilder.CreateIndex(
            name: "IX_StoreAuditLogs_OrganizationId_Date",
            table: "StoreAuditLogs",
            columns: new[] { "OrganizationId", "Date" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "IX_StoreConnections_OrganizationId_Store",
            table: "StoreConnections",
            columns: new[] { "OrganizationId", "Store" });

        migrationBuilder.CreateIndex(
            name: "IX_UserRoleRelations_RoleId",
            table: "UserRoleRelations",
            column: "RoleId");

        migrationBuilder.CreateIndex(
            name: "IX_UserRoleRelations_UserId",
            table: "UserRoleRelations",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_Users_NormalizedEmail",
            table: "Users",
            column: "NormalizedEmail",
            unique: true,
            filter: "\"NormalizedEmail\" <> ''");

        migrationBuilder.CreateIndex(
            name: "IX_Users_NormalizedUserName",
            table: "Users",
            column: "NormalizedUserName",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Users_UserName_IsDeleted_DeletionDate",
            table: "Users",
            columns: new[] { "UserName", "IsDeleted", "DeletionDate" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserSessions_UserId",
            table: "UserSessions",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_UserSessions_UserName_DeviceId",
            table: "UserSessions",
            columns: new[] { "UserName", "DeviceId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "_MigrationHistory");

        migrationBuilder.DropTable(
            name: "ActivityLogs");

        migrationBuilder.DropTable(
            name: "FeatureFlags");

        migrationBuilder.DropTable(
            name: "IdempotencyKeys");

        migrationBuilder.DropTable(
            name: "InternalNotifications");

        migrationBuilder.DropTable(
            name: "Languages");

        migrationBuilder.DropTable(
            name: "OrganizationInvitations");

        migrationBuilder.DropTable(
            name: "OrganizationMembers");

        migrationBuilder.DropTable(
            name: "RolePermissionRelations");

        migrationBuilder.DropTable(
            name: "StoreAuditLogs");

        migrationBuilder.DropTable(
            name: "StoreConnections");

        migrationBuilder.DropTable(
            name: "UserRoleRelations");

        migrationBuilder.DropTable(
            name: "UserSessionHistories");

        migrationBuilder.DropTable(
            name: "UserSessions");

        migrationBuilder.DropTable(
            name: "Permissions");

        migrationBuilder.DropTable(
            name: "Organizations");

        migrationBuilder.DropTable(
            name: "Roles");

        migrationBuilder.DropTable(
            name: "Users");
    }
}
