using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Storeasy.Domain.JsonModels;

#nullable disable

namespace Storeasy.Api.Migrations;

/// <inheritdoc />
public partial class _20261121164524_AddAsoKeywords : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AsoCompetitorRanks",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                TrackedKeywordId = table.Column<int>(type: "integer", nullable: false),
                Date = table.Column<DateOnly>(type: "date", nullable: false),
                CompetitorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                CompetitorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Rank = table.Column<int>(type: "integer", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AsoCompetitorRanks", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "AsoKeywordMetrics",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Store = table.Column<byte>(type: "smallint", nullable: false),
                Country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                Keyword = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Popularity = table.Column<int>(type: "integer", nullable: true),
                PopularitySource = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                Difficulty = table.Column<int>(type: "integer", nullable: true),
                CompetingApps = table.Column<int>(type: "integer", nullable: true),
                TopCompetitors = table.Column<List<AsoCompetitor>>(type: "jsonb", nullable: true),
                RelatedTerms = table.Column<string>(type: "jsonb", nullable: true),
                PopularityDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ResultsDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AsoKeywordMetrics", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "AsoKeywordRanks",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                TrackedKeywordId = table.Column<int>(type: "integer", nullable: false),
                StoreAppId = table.Column<int>(type: "integer", nullable: false),
                Store = table.Column<byte>(type: "smallint", nullable: false),
                Keyword = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                Date = table.Column<DateOnly>(type: "date", nullable: false),
                Rank = table.Column<int>(type: "integer", nullable: true),
                Depth = table.Column<int>(type: "integer", nullable: false),
                CheckedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AsoKeywordRanks", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "AsoTrackedKeywords",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                AppId = table.Column<int>(type: "integer", nullable: false),
                StoreAppId = table.Column<int>(type: "integer", nullable: false),
                Store = table.Column<byte>(type: "smallint", nullable: false),
                Keyword = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                TrackCompetitors = table.Column<bool>(type: "boolean", nullable: false),
                CompetitorIds = table.Column<string>(type: "jsonb", nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                TrackedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UntrackedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastCheckedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastRank = table.Column<int>(type: "integer", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AsoTrackedKeywords", x => x.Id);
                table.ForeignKey(
                    name: "FK_AsoTrackedKeywords_StoreApps_StoreAppId",
                    column: x => x.StoreAppId,
                    principalTable: "StoreApps",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AsoCompetitorRanks_TrackedKeywordId_Date_CompetitorId",
            table: "AsoCompetitorRanks",
            columns: new[] { "TrackedKeywordId", "Date", "CompetitorId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AsoKeywordMetrics_Store_Country_Keyword",
            table: "AsoKeywordMetrics",
            columns: new[] { "Store", "Country", "Keyword" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AsoKeywordRanks_StoreAppId_Keyword_Country_Date",
            table: "AsoKeywordRanks",
            columns: new[] { "StoreAppId", "Keyword", "Country", "Date" });

        migrationBuilder.CreateIndex(
            name: "IX_AsoKeywordRanks_TrackedKeywordId_Date",
            table: "AsoKeywordRanks",
            columns: new[] { "TrackedKeywordId", "Date" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AsoTrackedKeywords_IsActive_LastCheckedDate",
            table: "AsoTrackedKeywords",
            columns: new[] { "IsActive", "LastCheckedDate" });

        migrationBuilder.CreateIndex(
            name: "IX_AsoTrackedKeywords_OrganizationId_IsActive",
            table: "AsoTrackedKeywords",
            columns: new[] { "OrganizationId", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_AsoTrackedKeywords_StoreAppId_Keyword_Country",
            table: "AsoTrackedKeywords",
            columns: new[] { "StoreAppId", "Keyword", "Country" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AsoCompetitorRanks");

        migrationBuilder.DropTable(
            name: "AsoKeywordMetrics");

        migrationBuilder.DropTable(
            name: "AsoKeywordRanks");

        migrationBuilder.DropTable(
            name: "AsoTrackedKeywords");
    }
}
