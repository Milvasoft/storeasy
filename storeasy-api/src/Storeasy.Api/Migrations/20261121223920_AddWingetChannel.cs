using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Storeasy.Domain.JsonModels;

#nullable disable

namespace Storeasy.Api.Migrations;

/// <inheritdoc />
public partial class _20261121223920_AddWingetChannel : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "WingetPackages",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                StoreAppId = table.Column<int>(type: "integer", nullable: false),
                PackageIdentifier = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                LatestVersion = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                Versions = table.Column<string>(type: "jsonb", nullable: true),
                Publisher = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                PackageName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                DefaultLocale = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                Locales = table.Column<string>(type: "jsonb", nullable: true),
                ManifestVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                SyncedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                SyncError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WingetPackages", x => x.Id);
                table.ForeignKey(
                    name: "FK_WingetPackages_StoreApps_StoreAppId",
                    column: x => x.StoreAppId,
                    principalTable: "StoreApps",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "WingetPullRequests",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                StoreAppId = table.Column<int>(type: "integer", nullable: false),
                Number = table.Column<int>(type: "integer", nullable: false),
                Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                HeadBranch = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                HeadSha = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                PackageVersion = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                ChangeKind = table.Column<byte>(type: "smallint", nullable: true),
                State = table.Column<byte>(type: "smallint", nullable: false),
                IsOpen = table.Column<bool>(type: "boolean", nullable: false),
                Labels = table.Column<string>(type: "jsonb", nullable: true),
                Checks = table.Column<List<WingetCheck>>(type: "jsonb", nullable: true),
                Comments = table.Column<List<WingetComment>>(type: "jsonb", nullable: true),
                StatusHistory = table.Column<List<WingetStateChange>>(type: "jsonb", nullable: true),
                CreatedByStoreasy = table.Column<bool>(type: "boolean", nullable: false),
                PlanId = table.Column<long>(type: "bigint", nullable: true),
                OpenedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                MergedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ClosedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                SyncedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WingetPullRequests", x => x.Id);
                table.ForeignKey(
                    name: "FK_WingetPullRequests_StoreApps_StoreAppId",
                    column: x => x.StoreAppId,
                    principalTable: "StoreApps",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_WingetPackages_OrganizationId",
            table: "WingetPackages",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_WingetPackages_StoreAppId",
            table: "WingetPackages",
            column: "StoreAppId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_WingetPullRequests_OrganizationId_IsOpen",
            table: "WingetPullRequests",
            columns: new[] { "OrganizationId", "IsOpen" });

        migrationBuilder.CreateIndex(
            name: "IX_WingetPullRequests_StoreAppId_Number",
            table: "WingetPullRequests",
            columns: new[] { "StoreAppId", "Number" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "WingetPackages");

        migrationBuilder.DropTable(
            name: "WingetPullRequests");
    }
}
