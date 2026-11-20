using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storeasy.Api.Migrations;

/// <inheritdoc />
public partial class _20261120100955_AddMsSubmissionPublishMode : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "PublishDate",
            table: "MsSubmissions",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<byte>(
            name: "PublishMode",
            table: "MsSubmissions",
            type: "smallint",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PublishDate",
            table: "MsSubmissions");

        migrationBuilder.DropColumn(
            name: "PublishMode",
            table: "MsSubmissions");
    }
}
