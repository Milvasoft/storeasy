using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Storeasy.Api.Migrations;

/// <inheritdoc />
public partial class _20261121155904_AddStoreMetrics : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ReportBucket",
            table: "StoreConnections",
            type: "character varying(222)",
            maxLength: 222,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "StoreMetricFeeds",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                StoreAppId = table.Column<int>(type: "integer", nullable: false),
                Store = table.Column<byte>(type: "smallint", nullable: false),
                Status = table.Column<byte>(type: "smallint", nullable: false),
                ExternalRequestId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                EnabledDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                EnabledByUserName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                LastFetchedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastSucceededDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                EarliestDataDate = table.Column<DateOnly>(type: "date", nullable: true),
                LatestDataDate = table.Column<DateOnly>(type: "date", nullable: true),
                Marker = table.Column<DateOnly>(type: "date", nullable: true),
                LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                LastJobId = table.Column<long>(type: "bigint", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StoreMetricFeeds", x => x.Id);
                table.ForeignKey(
                    name: "FK_StoreMetricFeeds_StoreApps_StoreAppId",
                    column: x => x.StoreAppId,
                    principalTable: "StoreApps",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "StoreMetrics",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                StoreAppId = table.Column<int>(type: "integer", nullable: false),
                Store = table.Column<byte>(type: "smallint", nullable: false),
                Date = table.Column<DateOnly>(type: "date", nullable: false),
                Country = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                Source = table.Column<byte>(type: "smallint", nullable: false),
                Impressions = table.Column<long>(type: "bigint", nullable: true),
                PageViews = table.Column<long>(type: "bigint", nullable: true),
                Downloads = table.Column<long>(type: "bigint", nullable: true),
                Redownloads = table.Column<long>(type: "bigint", nullable: true),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StoreMetrics", x => x.Id);
                table.ForeignKey(
                    name: "FK_StoreMetrics_StoreApps_StoreAppId",
                    column: x => x.StoreAppId,
                    principalTable: "StoreApps",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_StoreMetricFeeds_OrganizationId",
            table: "StoreMetricFeeds",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_StoreMetricFeeds_StoreAppId",
            table: "StoreMetricFeeds",
            column: "StoreAppId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_StoreMetrics_OrganizationId_Date",
            table: "StoreMetrics",
            columns: new[] { "OrganizationId", "Date" });

        migrationBuilder.CreateIndex(
            name: "IX_StoreMetrics_StoreAppId_Date_Country_Source",
            table: "StoreMetrics",
            columns: new[] { "StoreAppId", "Date", "Country", "Source" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "StoreMetricFeeds");

        migrationBuilder.DropTable(
            name: "StoreMetrics");

        migrationBuilder.DropColumn(
            name: "ReportBucket",
            table: "StoreConnections");
    }
}
