using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Storeasy.Api.Migrations;

/// <inheritdoc />
public partial class _20261119144958_AddGooglePlayWebhooks : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PushServiceAccount",
            table: "WebhookEndpoints",
            type: "character varying(320)",
            maxLength: 320,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PushServiceAccount",
            table: "WebhookEndpoints");
    }
}
