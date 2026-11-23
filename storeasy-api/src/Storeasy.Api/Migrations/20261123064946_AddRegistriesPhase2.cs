using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Storeasy.Api.Migrations;

/// <inheritdoc />
public partial class _20261123064946_AddRegistriesPhase2 : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "SetupDate",
            table: "TrackedPackages",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SetupEnvironment",
            table: "TrackedPackages",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<byte>(
            name: "SetupProvider",
            table: "TrackedPackages",
            type: "smallint",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SetupRegistryUser",
            table: "TrackedPackages",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SetupRepository",
            table: "TrackedPackages",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SetupWorkflow",
            table: "TrackedPackages",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AlterColumn<int>(
            name: "TrackedPackageId",
            table: "PackageAlerts",
            type: "integer",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "integer");

        migrationBuilder.AddColumn<int>(
            name: "TokenReminderId",
            table: "PackageAlerts",
            type: "integer",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "RegistryTokenReminders",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                Registry = table.Column<byte>(type: "smallint", nullable: false),
                Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                UsedIn = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                TrackedPackageId = table.Column<int>(type: "integer", nullable: true),
                WarnedStage = table.Column<byte>(type: "smallint", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RegistryTokenReminders", x => x.Id);
                table.ForeignKey(
                    name: "FK_RegistryTokenReminders_TrackedPackages_TrackedPackageId",
                    column: x => x.TrackedPackageId,
                    principalTable: "TrackedPackages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PackageAlerts_TokenReminderId",
            table: "PackageAlerts",
            column: "TokenReminderId");

        migrationBuilder.CreateIndex(
            name: "IX_RegistryTokenReminders_ExpiryDate",
            table: "RegistryTokenReminders",
            column: "ExpiryDate");

        migrationBuilder.CreateIndex(
            name: "IX_RegistryTokenReminders_OrganizationId",
            table: "RegistryTokenReminders",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_RegistryTokenReminders_TrackedPackageId",
            table: "RegistryTokenReminders",
            column: "TrackedPackageId");

        migrationBuilder.AddForeignKey(
            name: "FK_PackageAlerts_RegistryTokenReminders_TokenReminderId",
            table: "PackageAlerts",
            column: "TokenReminderId",
            principalTable: "RegistryTokenReminders",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_PackageAlerts_RegistryTokenReminders_TokenReminderId",
            table: "PackageAlerts");

        migrationBuilder.DropTable(
            name: "RegistryTokenReminders");

        migrationBuilder.DropIndex(
            name: "IX_PackageAlerts_TokenReminderId",
            table: "PackageAlerts");

        migrationBuilder.DropColumn(
            name: "SetupDate",
            table: "TrackedPackages");

        migrationBuilder.DropColumn(
            name: "SetupEnvironment",
            table: "TrackedPackages");

        migrationBuilder.DropColumn(
            name: "SetupProvider",
            table: "TrackedPackages");

        migrationBuilder.DropColumn(
            name: "SetupRegistryUser",
            table: "TrackedPackages");

        migrationBuilder.DropColumn(
            name: "SetupRepository",
            table: "TrackedPackages");

        migrationBuilder.DropColumn(
            name: "SetupWorkflow",
            table: "TrackedPackages");

        migrationBuilder.DropColumn(
            name: "TokenReminderId",
            table: "PackageAlerts");

        migrationBuilder.AlterColumn<int>(
            name: "TrackedPackageId",
            table: "PackageAlerts",
            type: "integer",
            nullable: false,
            defaultValue: 0,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);
    }
}
