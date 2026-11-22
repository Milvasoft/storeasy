using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Storeasy.Domain.JsonModels;

#nullable disable

namespace Storeasy.Api.Migrations;

/// <inheritdoc />
public partial class _20261122001138_AddChromeChannel : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ChromeItems",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                StoreAppId = table.Column<int>(type: "integer", nullable: false),
                ItemId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                PublishedState = table.Column<byte>(type: "smallint", nullable: false),
                PublishedVersion = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                PublishedDeployPercentage = table.Column<int>(type: "integer", nullable: true),
                SubmittedState = table.Column<byte>(type: "smallint", nullable: false),
                SubmittedVersion = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                SubmittedDeployPercentage = table.Column<int>(type: "integer", nullable: true),
                LastUploadState = table.Column<byte>(type: "smallint", nullable: false),
                UploadedVersion = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                Warned = table.Column<bool>(type: "boolean", nullable: false),
                TakenDown = table.Column<bool>(type: "boolean", nullable: false),
                Rating = table.Column<double>(type: "double precision", nullable: true),
                RatingCount = table.Column<long>(type: "bigint", nullable: true),
                WeeklyUsers = table.Column<long>(type: "bigint", nullable: true),
                StatusHistory = table.Column<List<ChromeStateChange>>(type: "jsonb", nullable: true),
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
                table.PrimaryKey("PK_ChromeItems", x => x.Id);
                table.ForeignKey(
                    name: "FK_ChromeItems_StoreApps_StoreAppId",
                    column: x => x.StoreAppId,
                    principalTable: "StoreApps",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ChromeItems_OrganizationId",
            table: "ChromeItems",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_ChromeItems_StoreAppId",
            table: "ChromeItems",
            column: "StoreAppId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ChromeItems");
    }
}
