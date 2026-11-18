using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Storeasy.Domain.JsonModels;

#nullable disable

namespace Storeasy.Api.Migrations;

/// <inheritdoc />
public partial class _20261118145123_Phase5MicrosoftStore : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "SecretExpiryDate",
            table: "StoreConnections",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "MsAddons",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                StoreAppId = table.Column<int>(type: "integer", nullable: false),
                StoreId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                ProductId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Type = table.Column<byte>(type: "smallint", nullable: false),
                Lifetime = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                PriceId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                LastPublishedSubmissionId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                PendingSubmissionId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                ApiWritable = table.Column<bool>(type: "boolean", nullable: false),
                AssistedReason = table.Column<byte>(type: "smallint", nullable: true),
                SyncedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MsAddons", x => x.Id);
                table.ForeignKey(
                    name: "FK_MsAddons_StoreApps_StoreAppId",
                    column: x => x.StoreAppId,
                    principalTable: "StoreApps",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "MsFlightGroups",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                GroupId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                MemberCount = table.Column<int>(type: "integer", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MsFlightGroups", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "MsFlights",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                StoreAppId = table.Column<int>(type: "integer", nullable: false),
                FlightId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                FriendlyName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Rank = table.Column<int>(type: "integer", nullable: true),
                GroupIds = table.Column<string>(type: "jsonb", nullable: true),
                LastPublishedSubmissionId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                PendingSubmissionId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                SyncedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MsFlights", x => x.Id);
                table.ForeignKey(
                    name: "FK_MsFlights_StoreApps_StoreAppId",
                    column: x => x.StoreAppId,
                    principalTable: "StoreApps",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "MsInstallerPackages",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                StoreAppId = table.Column<int>(type: "integer", nullable: false),
                PackageId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                PackageUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                PackageType = table.Column<byte>(type: "smallint", nullable: false),
                Architectures = table.Column<string>(type: "jsonb", nullable: true),
                Languages = table.Column<string>(type: "jsonb", nullable: true),
                InstallerParameters = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                IsSilentInstall = table.Column<bool>(type: "boolean", nullable: false),
                GenericDocUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                SignatureStatus = table.Column<byte>(type: "smallint", nullable: false),
                SignatureNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CheckedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                SyncedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MsInstallerPackages", x => x.Id);
                table.ForeignKey(
                    name: "FK_MsInstallerPackages_StoreApps_StoreAppId",
                    column: x => x.StoreAppId,
                    principalTable: "StoreApps",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "MsProducts",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                StoreAppId = table.Column<int>(type: "integer", nullable: false),
                Kind = table.Column<byte>(type: "smallint", nullable: false),
                PricingV2 = table.Column<bool>(type: "boolean", nullable: false),
                FirstSubmissionDone = table.Column<bool>(type: "boolean", nullable: false),
                HasMandatoryUpdate = table.Column<bool>(type: "boolean", nullable: false),
                IsReady = table.Column<bool>(type: "boolean", nullable: false),
                LastPublishedSubmissionId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                PendingSubmissionId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                SyncedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MsProducts", x => x.Id);
                table.ForeignKey(
                    name: "FK_MsProducts_StoreApps_StoreAppId",
                    column: x => x.StoreAppId,
                    principalTable: "StoreApps",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "MsSubmissions",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                StoreAppId = table.Column<int>(type: "integer", nullable: false),
                Target = table.Column<byte>(type: "smallint", nullable: false),
                ExternalId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                FlightId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                AddonStoreId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                FriendlyName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                Status = table.Column<byte>(type: "smallint", nullable: false),
                StatusHistory = table.Column<List<MsStatusChange>>(type: "jsonb", nullable: true),
                StatusDetails = table.Column<MsStatusDetails>(type: "jsonb", nullable: true),
                IsPending = table.Column<bool>(type: "boolean", nullable: false),
                CreatedByStoreasy = table.Column<bool>(type: "boolean", nullable: false),
                ModifiedOutsideApi = table.Column<bool>(type: "boolean", nullable: false),
                ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                PackageVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                RolloutPercentage = table.Column<double>(type: "double precision", nullable: true),
                RolloutStatus = table.Column<byte>(type: "smallint", nullable: true),
                SyncedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MsSubmissions", x => x.Id);
                table.ForeignKey(
                    name: "FK_MsSubmissions_StoreApps_StoreAppId",
                    column: x => x.StoreAppId,
                    principalTable: "StoreApps",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_MsAddons_OrganizationId",
            table: "MsAddons",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_MsAddons_StoreAppId_StoreId",
            table: "MsAddons",
            columns: new[] { "StoreAppId", "StoreId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MsFlightGroups_OrganizationId_GroupId",
            table: "MsFlightGroups",
            columns: new[] { "OrganizationId", "GroupId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MsFlights_OrganizationId",
            table: "MsFlights",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_MsFlights_StoreAppId_FlightId",
            table: "MsFlights",
            columns: new[] { "StoreAppId", "FlightId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MsInstallerPackages_OrganizationId",
            table: "MsInstallerPackages",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_MsInstallerPackages_StoreAppId",
            table: "MsInstallerPackages",
            column: "StoreAppId");

        migrationBuilder.CreateIndex(
            name: "IX_MsProducts_OrganizationId",
            table: "MsProducts",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_MsProducts_StoreAppId",
            table: "MsProducts",
            column: "StoreAppId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_MsSubmissions_OrganizationId",
            table: "MsSubmissions",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_MsSubmissions_StoreAppId_Target_ExternalId",
            table: "MsSubmissions",
            columns: new[] { "StoreAppId", "Target", "ExternalId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "MsAddons");

        migrationBuilder.DropTable(
            name: "MsFlightGroups");

        migrationBuilder.DropTable(
            name: "MsFlights");

        migrationBuilder.DropTable(
            name: "MsInstallerPackages");

        migrationBuilder.DropTable(
            name: "MsProducts");

        migrationBuilder.DropTable(
            name: "MsSubmissions");

        migrationBuilder.DropColumn(
            name: "SecretExpiryDate",
            table: "StoreConnections");
    }
}
