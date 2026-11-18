using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Storeasy.Api.Migrations;

/// <inheritdoc />
public partial class _20261118123714_AddAppsAndBuilds : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Apps",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                IconUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                table.PrimaryKey("PK_Apps", x => x.Id);
                table.ForeignKey(
                    name: "FK_Apps_Organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "StoreApps",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                AppId = table.Column<int>(type: "integer", nullable: false),
                StoreConnectionId = table.Column<int>(type: "integer", nullable: true),
                Store = table.Column<byte>(type: "smallint", nullable: false),
                ExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                BundleId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                SyncedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StoreApps", x => x.Id);
                table.ForeignKey(
                    name: "FK_StoreApps_Apps_AppId",
                    column: x => x.AppId,
                    principalTable: "Apps",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_StoreApps_StoreConnections_StoreConnectionId",
                    column: x => x.StoreConnectionId,
                    principalTable: "StoreConnections",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "Builds",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                StoreAppId = table.Column<int>(type: "integer", nullable: false),
                Store = table.Column<byte>(type: "smallint", nullable: false),
                ExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                BuildNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                ProcessingState = table.Column<byte>(type: "smallint", nullable: false),
                Tracks = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                MinOsVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                UploadedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                SyncedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Builds", x => x.Id);
                table.ForeignKey(
                    name: "FK_Builds_StoreApps_StoreAppId",
                    column: x => x.StoreAppId,
                    principalTable: "StoreApps",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Apps_OrganizationId",
            table: "Apps",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_Builds_OrganizationId",
            table: "Builds",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_Builds_StoreAppId_ExternalId",
            table: "Builds",
            columns: new[] { "StoreAppId", "ExternalId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_StoreApps_AppId_Store",
            table: "StoreApps",
            columns: new[] { "AppId", "Store" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_StoreApps_OrganizationId_Store_ExternalId",
            table: "StoreApps",
            columns: new[] { "OrganizationId", "Store", "ExternalId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_StoreApps_StoreConnectionId",
            table: "StoreApps",
            column: "StoreConnectionId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Builds");

        migrationBuilder.DropTable(
            name: "StoreApps");

        migrationBuilder.DropTable(
            name: "Apps");
    }
}
