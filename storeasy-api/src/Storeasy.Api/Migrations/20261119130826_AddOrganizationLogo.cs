using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storeasy.Api.Migrations;

/// <inheritdoc />
public partial class _20261119130826_AddOrganizationLogo : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "LogoKey",
            table: "Organizations",
            type: "character varying(512)",
            maxLength: 512,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "LogoUpdateDate",
            table: "Organizations",
            type: "timestamp with time zone",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LogoKey",
            table: "Organizations");

        migrationBuilder.DropColumn(
            name: "LogoUpdateDate",
            table: "Organizations");
    }
}
