using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Storeasy.Api.Migrations;

/// <inheritdoc />
public partial class _20261119122541_AddOrganizationAiSettings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OrganizationAiSettings",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                EncryptedApiKey = table.Column<byte[]>(type: "bytea", nullable: true),
                EncryptedDataKey = table.Column<byte[]>(type: "bytea", nullable: true),
                KeyVersion = table.Column<int>(type: "integer", nullable: false),
                ApiKeyLastFour = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                ApiKeySetDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ApiKeySetByUserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrganizationAiSettings", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrganizationAiSettings_Organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationAiSettings_OrganizationId",
            table: "OrganizationAiSettings",
            column: "OrganizationId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "OrganizationAiSettings");
    }
}
