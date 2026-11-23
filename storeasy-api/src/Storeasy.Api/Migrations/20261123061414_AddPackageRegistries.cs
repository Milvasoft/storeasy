using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Storeasy.Api.Migrations;

/// <inheritdoc />
public partial class _20261123061414_AddPackageRegistries : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TrackedPackages",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                Registry = table.Column<byte>(type: "smallint", nullable: false),
                Name = table.Column<string>(type: "character varying(214)", maxLength: 214, nullable: true),
                AppId = table.Column<int>(type: "integer", nullable: true),
                LatestVersion = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                LatestPublishedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                SourceRepository = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                LatestCiRunUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                LatestWorkflow = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                HasProvenance = table.Column<bool>(type: "boolean", nullable: false),
                DeprecationMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                SyncedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                SyncError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TrackedPackages", x => x.Id);
                table.ForeignKey(
                    name: "FK_TrackedPackages_Apps_AppId",
                    column: x => x.AppId,
                    principalTable: "Apps",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "PackageAlerts",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                TrackedPackageId = table.Column<int>(type: "integer", nullable: false),
                Kind = table.Column<byte>(type: "smallint", nullable: false),
                Version = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                AdvisoryId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                OpenedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ResolvedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PackageAlerts", x => x.Id);
                table.ForeignKey(
                    name: "FK_PackageAlerts_TrackedPackages_TrackedPackageId",
                    column: x => x.TrackedPackageId,
                    principalTable: "TrackedPackages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "TrackedPackageVersions",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                TrackedPackageId = table.Column<int>(type: "integer", nullable: false),
                Version = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                PublishedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsYanked = table.Column<bool>(type: "boolean", nullable: false),
                IsUnlisted = table.Column<bool>(type: "boolean", nullable: false),
                IsDeprecated = table.Column<bool>(type: "boolean", nullable: false),
                Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                HasProvenance = table.Column<bool>(type: "boolean", nullable: true),
                CiRunUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TrackedPackageVersions", x => x.Id);
                table.ForeignKey(
                    name: "FK_TrackedPackageVersions_TrackedPackages_TrackedPackageId",
                    column: x => x.TrackedPackageId,
                    principalTable: "TrackedPackages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PackageAlerts_OrganizationId_ResolvedDate",
            table: "PackageAlerts",
            columns: new[] { "OrganizationId", "ResolvedDate" });

        migrationBuilder.CreateIndex(
            name: "IX_PackageAlerts_TrackedPackageId_ResolvedDate",
            table: "PackageAlerts",
            columns: new[] { "TrackedPackageId", "ResolvedDate" });

        migrationBuilder.CreateIndex(
            name: "IX_TrackedPackages_AppId",
            table: "TrackedPackages",
            column: "AppId");

        migrationBuilder.CreateIndex(
            name: "IX_TrackedPackages_OrganizationId_Registry_Name",
            table: "TrackedPackages",
            columns: new[] { "OrganizationId", "Registry", "Name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TrackedPackages_SyncedDate",
            table: "TrackedPackages",
            column: "SyncedDate");

        migrationBuilder.CreateIndex(
            name: "IX_TrackedPackageVersions_TrackedPackageId_Version",
            table: "TrackedPackageVersions",
            columns: new[] { "TrackedPackageId", "Version" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "PackageAlerts");

        migrationBuilder.DropTable(
            name: "TrackedPackageVersions");

        migrationBuilder.DropTable(
            name: "TrackedPackages");
    }
}
