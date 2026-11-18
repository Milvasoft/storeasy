using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Storeasy.Domain.JsonModels;

#nullable disable

namespace Storeasy.Api.Migrations;

/// <inheritdoc />
public partial class _20261118133903_AddContentPlansAnswersAndSteps : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<ReadinessSummary>(
            name: "LastReadiness",
            table: "StoreApps",
            type: "jsonb",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "UsesNonExemptEncryption",
            table: "Builds",
            type: "boolean",
            nullable: true);

        migrationBuilder.AddColumn<AppProfile>(
            name: "Profile",
            table: "Apps",
            type: "jsonb",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "AssistedSteps",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                AppId = table.Column<int>(type: "integer", nullable: false),
                Store = table.Column<byte>(type: "smallint", nullable: false),
                Key = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                Status = table.Column<byte>(type: "smallint", nullable: false),
                DoneItems = table.Column<string>(type: "jsonb", nullable: true),
                HandedOffDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                HandedOffByUserName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                CompletedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CompletedByUserName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                CompletedChannel = table.Column<byte>(type: "smallint", nullable: true),
                EvidenceAssetId = table.Column<long>(type: "bigint", nullable: true),
                Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AssistedSteps", x => x.Id);
                table.ForeignKey(
                    name: "FK_AssistedSteps_Apps_AppId",
                    column: x => x.AppId,
                    principalTable: "Apps",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ChangePlans",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                AppId = table.Column<int>(type: "integer", nullable: false),
                StoreAppId = table.Column<int>(type: "integer", nullable: false),
                Store = table.Column<byte>(type: "smallint", nullable: false),
                Kind = table.Column<byte>(type: "smallint", nullable: false),
                Status = table.Column<byte>(type: "smallint", nullable: false),
                Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Source = table.Column<byte>(type: "smallint", nullable: false),
                CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                CreatedByUserName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                RequiredPermission = table.Column<byte>(type: "smallint", nullable: false),
                ConfirmText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                VersionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                VersionString = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Items = table.Column<List<PlanItem>>(type: "jsonb", nullable: true),
                Warnings = table.Column<List<PlanWarning>>(type: "jsonb", nullable: true),
                Calls = table.Column<List<PlanCall>>(type: "jsonb", nullable: true),
                Files = table.Column<List<PlanFile>>(type: "jsonb", nullable: true),
                Changes = table.Column<string>(type: "jsonb", nullable: true),
                BaseKeys = table.Column<string>(type: "jsonb", nullable: true),
                BaseHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                CatalogVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                DecidedByUserId = table.Column<int>(type: "integer", nullable: true),
                DecidedByUserName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                DecisionChannel = table.Column<byte>(type: "smallint", nullable: true),
                DecisionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                DecisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                StoreJobId = table.Column<long>(type: "bigint", nullable: true),
                AppliedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ChangePlans", x => x.Id);
                table.ForeignKey(
                    name: "FK_ChangePlans_StoreApps_StoreAppId",
                    column: x => x.StoreAppId,
                    principalTable: "StoreApps",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "SavedAnswers",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                AppId = table.Column<int>(type: "integer", nullable: false),
                Kind = table.Column<byte>(type: "smallint", nullable: false),
                Answers = table.Column<string>(type: "jsonb", nullable: true),
                EncryptedSecret = table.Column<byte[]>(type: "bytea", nullable: true),
                EncryptedDataKey = table.Column<byte[]>(type: "bytea", nullable: true),
                KeyVersion = table.Column<int>(type: "integer", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SavedAnswers", x => x.Id);
                table.ForeignKey(
                    name: "FK_SavedAnswers_Apps_AppId",
                    column: x => x.AppId,
                    principalTable: "Apps",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AssistedSteps_AppId_Store_Key",
            table: "AssistedSteps",
            columns: new[] { "AppId", "Store", "Key" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AssistedSteps_OrganizationId",
            table: "AssistedSteps",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_ChangePlans_OrganizationId_Status",
            table: "ChangePlans",
            columns: new[] { "OrganizationId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_ChangePlans_StoreAppId",
            table: "ChangePlans",
            column: "StoreAppId");

        migrationBuilder.CreateIndex(
            name: "IX_SavedAnswers_AppId_Kind",
            table: "SavedAnswers",
            columns: new[] { "AppId", "Kind" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SavedAnswers_OrganizationId",
            table: "SavedAnswers",
            column: "OrganizationId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AssistedSteps");

        migrationBuilder.DropTable(
            name: "ChangePlans");

        migrationBuilder.DropTable(
            name: "SavedAnswers");

        migrationBuilder.DropColumn(
            name: "LastReadiness",
            table: "StoreApps");

        migrationBuilder.DropColumn(
            name: "UsesNonExemptEncryption",
            table: "Builds");

        migrationBuilder.DropColumn(
            name: "Profile",
            table: "Apps");
    }
}
