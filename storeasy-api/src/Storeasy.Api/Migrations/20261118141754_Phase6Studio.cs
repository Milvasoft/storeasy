using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Storeasy.Domain.JsonModels;

#nullable disable

namespace Storeasy.Api.Migrations;

/// <inheritdoc />
public partial class _20261118141754_Phase6Studio : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "MockupTemplates",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                AppId = table.Column<int>(type: "integer", nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                PrimaryLocale = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                Locales = table.Column<List<string>>(type: "text[]", nullable: true),
                SlotCount = table.Column<int>(type: "integer", nullable: false),
                Spec = table.Column<MockupSpec>(type: "jsonb", nullable: true),
                StarterKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                Version = table.Column<int>(type: "integer", nullable: false),
                LastExportJobId = table.Column<long>(type: "bigint", nullable: true),
                LastExportDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastExportImageCount = table.Column<int>(type: "integer", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true),
                DeletionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeleterUserName = table.Column<string>(type: "text", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MockupTemplates", x => x.Id);
                table.ForeignKey(
                    name: "FK_MockupTemplates_Apps_AppId",
                    column: x => x.AppId,
                    principalTable: "Apps",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_MockupTemplates_Organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "MockupCaptions",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                MockupTemplateId = table.Column<int>(type: "integer", nullable: false),
                Locale = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                SlotOrder = table.Column<int>(type: "integer", nullable: false),
                Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Subtitle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MockupCaptions", x => x.Id);
                table.ForeignKey(
                    name: "FK_MockupCaptions_MockupTemplates_MockupTemplateId",
                    column: x => x.MockupTemplateId,
                    principalTable: "MockupTemplates",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "MockupSlots",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                MockupTemplateId = table.Column<int>(type: "integer", nullable: false),
                Order = table.Column<int>(type: "integer", nullable: false),
                Family = table.Column<byte>(type: "smallint", nullable: false),
                Locale = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                AssetId = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MockupSlots", x => x.Id);
                table.ForeignKey(
                    name: "FK_MockupSlots_MockupTemplates_MockupTemplateId",
                    column: x => x.MockupTemplateId,
                    principalTable: "MockupTemplates",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_MockupCaptions_MockupTemplateId_Locale_SlotOrder",
            table: "MockupCaptions",
            columns: new[] { "MockupTemplateId", "Locale", "SlotOrder" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MockupSlots_MockupTemplateId_Order",
            table: "MockupSlots",
            columns: new[] { "MockupTemplateId", "Order" });

        migrationBuilder.CreateIndex(
            name: "IX_MockupTemplates_AppId",
            table: "MockupTemplates",
            column: "AppId");

        migrationBuilder.CreateIndex(
            name: "IX_MockupTemplates_OrganizationId_AppId",
            table: "MockupTemplates",
            columns: new[] { "OrganizationId", "AppId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "MockupCaptions");

        migrationBuilder.DropTable(
            name: "MockupSlots");

        migrationBuilder.DropTable(
            name: "MockupTemplates");
    }
}
