using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Storeasy.Domain.JsonModels;

#nullable disable

namespace Storeasy.Api.Migrations;

/// <inheritdoc />
public partial class _20261122144905_AddFirefoxChannel : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "FirefoxAddons",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                StoreAppId = table.Column<int>(type: "integer", nullable: false),
                AddonId = table.Column<long>(type: "bigint", nullable: false),
                Guid = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                Slug = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                Status = table.Column<byte>(type: "smallint", nullable: false),
                IsDisabled = table.Column<bool>(type: "boolean", nullable: false),
                DefaultLocale = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                ListedVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                LatestVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                LatestVersionId = table.Column<long>(type: "bigint", nullable: true),
                LatestChannel = table.Column<byte>(type: "smallint", nullable: false),
                LatestFileStatus = table.Column<byte>(type: "smallint", nullable: false),
                Rating = table.Column<double>(type: "double precision", nullable: true),
                RatingCount = table.Column<long>(type: "bigint", nullable: true),
                AverageDailyUsers = table.Column<long>(type: "bigint", nullable: true),
                WeeklyDownloads = table.Column<long>(type: "bigint", nullable: true),
                StatusHistory = table.Column<List<FirefoxStateChange>>(type: "jsonb", nullable: true),
                SyncedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                SyncError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                IsPending = table.Column<bool>(type: "boolean", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FirefoxAddons", x => x.Id);
                table.ForeignKey(
                    name: "FK_FirefoxAddons_StoreApps_StoreAppId",
                    column: x => x.StoreAppId,
                    principalTable: "StoreApps",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_FirefoxAddons_OrganizationId",
            table: "FirefoxAddons",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_FirefoxAddons_StoreAppId",
            table: "FirefoxAddons",
            column: "StoreAppId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "FirefoxAddons");
    }
}
