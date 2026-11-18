using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Storeasy.Api.Migrations;

/// <inheritdoc />
public partial class _20261118182350_Phase8Final : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "UserAgent",
            table: "UserSessions",
            type: "character varying(300)",
            maxLength: 300,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PreferredLanguage",
            table: "Users",
            type: "character varying(10)",
            maxLength: 10,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "AuditRetentionDays",
            table: "Organizations",
            type: "integer",
            nullable: false,
            defaultValue: 365);

        migrationBuilder.AddColumn<string>(
            name: "DefaultLocale",
            table: "Organizations",
            type: "character varying(10)",
            maxLength: 10,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "CustomRoleId",
            table: "OrganizationMembers",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "CustomRoleId",
            table: "OrganizationInvitations",
            type: "integer",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "EmailOutbox",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Kind = table.Column<byte>(type: "smallint", nullable: false),
                Language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                EncryptedPayload = table.Column<byte[]>(type: "bytea", nullable: true),
                EncryptedDataKey = table.Column<byte[]>(type: "bytea", nullable: true),
                KeyVersion = table.Column<int>(type: "integer", nullable: false),
                Attempt = table.Column<int>(type: "integer", nullable: false),
                NextAttemptDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LockedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EmailOutbox", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "OrganizationRoles",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Permissions = table.Column<string>(type: "jsonb", nullable: true),
                AudienceRole = table.Column<byte>(type: "smallint", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationRoles", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationRoles_Organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "SystemSettings",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Value = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SystemSettings", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "UserExternalLogins",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<int>(type: "integer", nullable: false),
                Provider = table.Column<byte>(type: "smallint", nullable: false),
                Subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                LastLoginDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserExternalLogins", x => x.Id);
                table.ForeignKey(
                    name: "FK_UserExternalLogins_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "WebPushSubscriptions",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<int>(type: "integer", nullable: false),
                Endpoint = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                P256dh = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Auth = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                UserAgent = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                LastSuccessDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WebPushSubscriptions", x => x.Id);
                table.ForeignKey(
                    name: "FK_WebPushSubscriptions_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationMembers_CustomRoleId",
            table: "OrganizationMembers",
            column: "CustomRoleId");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationInvitations_CustomRoleId",
            table: "OrganizationInvitations",
            column: "CustomRoleId");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationInvitations_InviterUserId",
            table: "OrganizationInvitations",
            column: "InviterUserId");

        migrationBuilder.CreateIndex(
            name: "IX_EmailOutbox_NextAttemptDate",
            table: "EmailOutbox",
            column: "NextAttemptDate");

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationRoles_OrganizationId_Name",
            table: "OrganizationRoles",
            columns: new[] { "OrganizationId", "Name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SystemSettings_Key",
            table: "SystemSettings",
            column: "Key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserExternalLogins_Provider_Subject",
            table: "UserExternalLogins",
            columns: new[] { "Provider", "Subject" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserExternalLogins_UserId",
            table: "UserExternalLogins",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_WebPushSubscriptions_Endpoint",
            table: "WebPushSubscriptions",
            column: "Endpoint",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_WebPushSubscriptions_UserId",
            table: "WebPushSubscriptions",
            column: "UserId");

        migrationBuilder.AddForeignKey(
            name: "FK_OrganizationInvitations_OrganizationRoles_CustomRoleId",
            table: "OrganizationInvitations",
            column: "CustomRoleId",
            principalTable: "OrganizationRoles",
            principalColumn: "Id");

        migrationBuilder.AddForeignKey(
            name: "FK_OrganizationInvitations_Users_InviterUserId",
            table: "OrganizationInvitations",
            column: "InviterUserId",
            principalTable: "Users",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_OrganizationMembers_OrganizationRoles_CustomRoleId",
            table: "OrganizationMembers",
            column: "CustomRoleId",
            principalTable: "OrganizationRoles",
            principalColumn: "Id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_OrganizationInvitations_OrganizationRoles_CustomRoleId",
            table: "OrganizationInvitations");

        migrationBuilder.DropForeignKey(
            name: "FK_OrganizationInvitations_Users_InviterUserId",
            table: "OrganizationInvitations");

        migrationBuilder.DropForeignKey(
            name: "FK_OrganizationMembers_OrganizationRoles_CustomRoleId",
            table: "OrganizationMembers");

        migrationBuilder.DropTable(
            name: "EmailOutbox");

        migrationBuilder.DropTable(
            name: "OrganizationRoles");

        migrationBuilder.DropTable(
            name: "SystemSettings");

        migrationBuilder.DropTable(
            name: "UserExternalLogins");

        migrationBuilder.DropTable(
            name: "WebPushSubscriptions");

        migrationBuilder.DropIndex(
            name: "IX_OrganizationMembers_CustomRoleId",
            table: "OrganizationMembers");

        migrationBuilder.DropIndex(
            name: "IX_OrganizationInvitations_CustomRoleId",
            table: "OrganizationInvitations");

        migrationBuilder.DropIndex(
            name: "IX_OrganizationInvitations_InviterUserId",
            table: "OrganizationInvitations");

        migrationBuilder.DropColumn(
            name: "UserAgent",
            table: "UserSessions");

        migrationBuilder.DropColumn(
            name: "PreferredLanguage",
            table: "Users");

        migrationBuilder.DropColumn(
            name: "AuditRetentionDays",
            table: "Organizations");

        migrationBuilder.DropColumn(
            name: "DefaultLocale",
            table: "Organizations");

        migrationBuilder.DropColumn(
            name: "CustomRoleId",
            table: "OrganizationMembers");

        migrationBuilder.DropColumn(
            name: "CustomRoleId",
            table: "OrganizationInvitations");
    }
}
