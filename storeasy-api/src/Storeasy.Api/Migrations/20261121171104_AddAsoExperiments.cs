using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Storeasy.Domain.JsonModels;

#nullable disable

namespace Storeasy.Api.Migrations;

/// <inheritdoc />
public partial class _20261121171104_AddAsoExperiments : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AsoExperiments",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                AppId = table.Column<int>(type: "integer", nullable: false),
                StoreAppId = table.Column<int>(type: "integer", nullable: false),
                Store = table.Column<byte>(type: "smallint", nullable: false),
                Kind = table.Column<byte>(type: "smallint", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Status = table.Column<byte>(type: "smallint", nullable: false),
                StoreState = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                ExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                ExternalVersionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                TrafficProportion = table.Column<int>(type: "integer", nullable: true),
                ReviewRequired = table.Column<bool>(type: "boolean", nullable: true),
                Variants = table.Column<List<AsoExperimentVariant>>(type: "jsonb", nullable: true),
                StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                ResultSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                ResultUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                WinnerVariantKey = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                ChecklistDone = table.Column<string>(type: "jsonb", nullable: true),
                PlanIds = table.Column<string>(type: "jsonb", nullable: true),
                SubmittedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                SyncedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AsoExperiments", x => x.Id);
                table.ForeignKey(
                    name: "FK_AsoExperiments_StoreApps_StoreAppId",
                    column: x => x.StoreAppId,
                    principalTable: "StoreApps",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AsoExperiments_OrganizationId_AppId",
            table: "AsoExperiments",
            columns: new[] { "OrganizationId", "AppId" });

        migrationBuilder.CreateIndex(
            name: "IX_AsoExperiments_StoreAppId_ExternalId",
            table: "AsoExperiments",
            columns: new[] { "StoreAppId", "ExternalId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AsoExperiments");
    }
}
