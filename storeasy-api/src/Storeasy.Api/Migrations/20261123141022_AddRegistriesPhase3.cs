using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Storeasy.Api.Migrations;

/// <inheritdoc />
public partial class _20261123141022_AddRegistriesPhase3 : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "Downloads",
            table: "TrackedPackageVersions",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "DownloadsSyncedDate",
            table: "TrackedPackages",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "Likes",
            table: "TrackedPackages",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "MaxPoints",
            table: "TrackedPackages",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "PackageGroupId",
            table: "TrackedPackages",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "Points",
            table: "TrackedPackages",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "RecentDownloads",
            table: "TrackedPackages",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "RecentDownloadsDays",
            table: "TrackedPackages",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "TotalDownloads",
            table: "TrackedPackages",
            type: "bigint",
            nullable: true);

        migrationBuilder.AddColumn<byte>(
            name: "VersionDownloadsScope",
            table: "TrackedPackages",
            type: "smallint",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "PackageDownloadDays",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                TrackedPackageId = table.Column<int>(type: "integer", nullable: false),
                Day = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Downloads = table.Column<long>(type: "bigint", nullable: true),
                TotalDownloads = table.Column<long>(type: "bigint", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PackageDownloadDays", x => x.Id);
                table.ForeignKey(
                    name: "FK_PackageDownloadDays_TrackedPackages_TrackedPackageId",
                    column: x => x.TrackedPackageId,
                    principalTable: "TrackedPackages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PackageGroups",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PackageGroups", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_TrackedPackages_PackageGroupId",
            table: "TrackedPackages",
            column: "PackageGroupId");

        migrationBuilder.CreateIndex(
            name: "IX_PackageDownloadDays_TrackedPackageId_Day",
            table: "PackageDownloadDays",
            columns: new[] { "TrackedPackageId", "Day" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PackageGroups_OrganizationId_Name",
            table: "PackageGroups",
            columns: new[] { "OrganizationId", "Name" },
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_TrackedPackages_PackageGroups_PackageGroupId",
            table: "TrackedPackages",
            column: "PackageGroupId",
            principalTable: "PackageGroups",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_TrackedPackages_PackageGroups_PackageGroupId",
            table: "TrackedPackages");

        migrationBuilder.DropTable(
            name: "PackageDownloadDays");

        migrationBuilder.DropTable(
            name: "PackageGroups");

        migrationBuilder.DropIndex(
            name: "IX_TrackedPackages_PackageGroupId",
            table: "TrackedPackages");

        migrationBuilder.DropColumn(
            name: "Downloads",
            table: "TrackedPackageVersions");

        migrationBuilder.DropColumn(
            name: "DownloadsSyncedDate",
            table: "TrackedPackages");

        migrationBuilder.DropColumn(
            name: "Likes",
            table: "TrackedPackages");

        migrationBuilder.DropColumn(
            name: "MaxPoints",
            table: "TrackedPackages");

        migrationBuilder.DropColumn(
            name: "PackageGroupId",
            table: "TrackedPackages");

        migrationBuilder.DropColumn(
            name: "Points",
            table: "TrackedPackages");

        migrationBuilder.DropColumn(
            name: "RecentDownloads",
            table: "TrackedPackages");

        migrationBuilder.DropColumn(
            name: "RecentDownloadsDays",
            table: "TrackedPackages");

        migrationBuilder.DropColumn(
            name: "TotalDownloads",
            table: "TrackedPackages");

        migrationBuilder.DropColumn(
            name: "VersionDownloadsScope",
            table: "TrackedPackages");
    }
}
