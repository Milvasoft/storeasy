using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Storeasy.Domain.JsonModels;

#nullable disable

namespace Storeasy.Api.Migrations;

/// <inheritdoc />
public partial class _20261118151224_Phase7Monetization : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ProductGroups",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                AppId = table.Column<int>(type: "integer", nullable: false),
                StoreAppId = table.Column<int>(type: "integer", nullable: false),
                Store = table.Column<byte>(type: "smallint", nullable: false),
                ExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                ReferenceName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                Localizations = table.Column<List<ProductLocalization>>(type: "jsonb", nullable: true),
                SyncedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProductGroups", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProductGroups_StoreApps_StoreAppId",
                    column: x => x.StoreAppId,
                    principalTable: "StoreApps",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "StoreProducts",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                AppId = table.Column<int>(type: "integer", nullable: false),
                StoreAppId = table.Column<int>(type: "integer", nullable: false),
                Store = table.Column<byte>(type: "smallint", nullable: false),
                Kind = table.Column<byte>(type: "smallint", nullable: false),
                ExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                ProductId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                ReferenceName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                GroupId = table.Column<long>(type: "bigint", nullable: true),
                GroupExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Level = table.Column<int>(type: "integer", nullable: true),
                Duration = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                State = table.Column<byte>(type: "smallint", nullable: false),
                StoreState = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                FamilySharable = table.Column<bool>(type: "boolean", nullable: true),
                ReviewNote = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                ReviewScreenshot = table.Column<ProductScreenshot>(type: "jsonb", nullable: true),
                Localizations = table.Column<List<ProductLocalization>>(type: "jsonb", nullable: true),
                Availability = table.Column<ProductAvailability>(type: "jsonb", nullable: true),
                BaseTerritory = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                Prices = table.Column<List<ProductPrice>>(type: "jsonb", nullable: true),
                Offers = table.Column<List<ProductOffer>>(type: "jsonb", nullable: true),
                Plans = table.Column<List<ProductPlan>>(type: "jsonb", nullable: true),
                RetentionMessage = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                MultiSeatStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                SyncedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StoreProducts", x => x.Id);
                table.ForeignKey(
                    name: "FK_StoreProducts_ProductGroups_GroupId",
                    column: x => x.GroupId,
                    principalTable: "ProductGroups",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_StoreProducts_StoreApps_StoreAppId",
                    column: x => x.StoreAppId,
                    principalTable: "StoreApps",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ProductGroups_OrganizationId",
            table: "ProductGroups",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_ProductGroups_StoreAppId_ExternalId",
            table: "ProductGroups",
            columns: new[] { "StoreAppId", "ExternalId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_StoreProducts_GroupId",
            table: "StoreProducts",
            column: "GroupId");

        migrationBuilder.CreateIndex(
            name: "IX_StoreProducts_OrganizationId_Kind",
            table: "StoreProducts",
            columns: new[] { "OrganizationId", "Kind" });

        migrationBuilder.CreateIndex(
            name: "IX_StoreProducts_StoreAppId_ExternalId",
            table: "StoreProducts",
            columns: new[] { "StoreAppId", "ExternalId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "StoreProducts");

        migrationBuilder.DropTable(
            name: "ProductGroups");
    }
}
