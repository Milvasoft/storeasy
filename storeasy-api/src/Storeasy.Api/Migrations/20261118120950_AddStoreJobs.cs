using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Storeasy.Api.Migrations;

/// <inheritdoc />
public partial class _20261118120950_AddStoreJobs : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "StoreJobs",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                AppId = table.Column<int>(type: "integer", nullable: true),
                Store = table.Column<byte>(type: "smallint", nullable: true),
                Kind = table.Column<short>(type: "smallint", nullable: false),
                Payload = table.Column<string>(type: "jsonb", nullable: true),
                Result = table.Column<string>(type: "jsonb", nullable: true),
                Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                Status = table.Column<byte>(type: "smallint", nullable: false),
                Channel = table.Column<byte>(type: "smallint", nullable: false),
                RequestedByUserId = table.Column<int>(type: "integer", nullable: true),
                RequestedByUserName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Attempt = table.Column<int>(type: "integer", nullable: false),
                MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                NextAttemptDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LockedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CancelRequested = table.Column<bool>(type: "boolean", nullable: false),
                Error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StoreJobs", x => x.Id);
                table.ForeignKey(
                    name: "FK_StoreJobs_Organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "StoreJobSteps",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                StoreJobId = table.Column<long>(type: "bigint", nullable: false),
                Order = table.Column<int>(type: "integer", nullable: false),
                Attempt = table.Column<int>(type: "integer", nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Direction = table.Column<byte>(type: "smallint", nullable: false),
                Status = table.Column<byte>(type: "smallint", nullable: false),
                Progress = table.Column<long>(type: "bigint", nullable: true),
                Total = table.Column<long>(type: "bigint", nullable: true),
                Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StoreJobSteps", x => x.Id);
                table.ForeignKey(
                    name: "FK_StoreJobSteps_StoreJobs_StoreJobId",
                    column: x => x.StoreJobId,
                    principalTable: "StoreJobs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_StoreJobs_OrganizationId_CreationDate",
            table: "StoreJobs",
            columns: new[] { "OrganizationId", "CreationDate" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "IX_StoreJobs_Status_NextAttemptDate",
            table: "StoreJobs",
            columns: new[] { "Status", "NextAttemptDate" });

        migrationBuilder.CreateIndex(
            name: "IX_StoreJobSteps_StoreJobId_Order",
            table: "StoreJobSteps",
            columns: new[] { "StoreJobId", "Order" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "StoreJobSteps");

        migrationBuilder.DropTable(
            name: "StoreJobs");
    }
}
