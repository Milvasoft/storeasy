using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Storeasy.Domain.JsonModels;

#nullable disable

namespace Storeasy.Api.Migrations;

/// <inheritdoc />
public partial class _20261119145700_AddPreReviews : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PreReviewModel",
            table: "OrganizationAiSettings",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ReplyDraftModel",
            table: "OrganizationAiSettings",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "PreReviews",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                AppId = table.Column<int>(type: "integer", nullable: false),
                StoreAppId = table.Column<int>(type: "integer", nullable: false),
                Store = table.Column<byte>(type: "smallint", nullable: false),
                VersionString = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                SnapshotHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                Status = table.Column<byte>(type: "smallint", nullable: false),
                JobId = table.Column<long>(type: "bigint", nullable: true),
                Model = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                Language = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                Verdict = table.Column<byte>(type: "smallint", nullable: true),
                ApprovalLikelihood = table.Column<int>(type: "integer", nullable: true),
                Summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                Findings = table.Column<List<PreReviewFinding>>(type: "jsonb", nullable: true),
                RuntimeChecks = table.Column<string>(type: "jsonb", nullable: true),
                PositiveNotes = table.Column<string>(type: "jsonb", nullable: true),
                ImageCount = table.Column<int>(type: "integer", nullable: false),
                InputTokens = table.Column<long>(type: "bigint", nullable: false),
                OutputTokens = table.Column<long>(type: "bigint", nullable: false),
                CacheReadInputTokens = table.Column<long>(type: "bigint", nullable: false),
                CacheCreationInputTokens = table.Column<long>(type: "bigint", nullable: false),
                EstimatedCostUsd = table.Column<decimal>(type: "numeric(12,6)", nullable: false),
                ErrorKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                CompletedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PreReviews", x => x.Id);
                table.ForeignKey(
                    name: "FK_PreReviews_Apps_AppId",
                    column: x => x.AppId,
                    principalTable: "Apps",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PreReviews_AppId",
            table: "PreReviews",
            column: "AppId");

        migrationBuilder.CreateIndex(
            name: "IX_PreReviews_OrganizationId_AppId_Store_CreationDate",
            table: "PreReviews",
            columns: new[] { "OrganizationId", "AppId", "Store", "CreationDate" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "PreReviews");

        migrationBuilder.DropColumn(
            name: "PreReviewModel",
            table: "OrganizationAiSettings");

        migrationBuilder.DropColumn(
            name: "ReplyDraftModel",
            table: "OrganizationAiSettings");
    }
}
