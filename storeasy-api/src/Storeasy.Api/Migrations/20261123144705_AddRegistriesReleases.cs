using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Storeasy.Domain.JsonModels;

#nullable disable

namespace Storeasy.Api.Migrations;

/// <inheritdoc />
public partial class _20261123144705_AddRegistriesReleases : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<ReleaseSettings>(
            name: "Release",
            table: "TrackedPackages",
            type: "jsonb",
            nullable: true);

        migrationBuilder.AddColumn<ReleaseSettings>(
            name: "Release",
            table: "PackageGroups",
            type: "jsonb",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "RegistryConnections",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                Kind = table.Column<byte>(type: "smallint", nullable: false),
                Fingerprint = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                EncryptedSecret = table.Column<byte[]>(type: "bytea", nullable: true),
                EncryptedDataKey = table.Column<byte[]>(type: "bytea", nullable: true),
                KeyVersion = table.Column<int>(type: "integer", nullable: false),
                Status = table.Column<byte>(type: "smallint", nullable: false),
                LastVerifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastVerifyError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                TokenReminderId = table.Column<int>(type: "integer", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RegistryConnections", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "RegistryPlans",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                TrackedPackageId = table.Column<int>(type: "integer", nullable: true),
                PackageGroupId = table.Column<int>(type: "integer", nullable: true),
                Kind = table.Column<byte>(type: "smallint", nullable: false),
                Status = table.Column<byte>(type: "smallint", nullable: false),
                Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                Version = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                CurrentVersion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Tag = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                ConfirmText = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                RequiredPermission = table.Column<byte>(type: "smallint", nullable: false),
                Release = table.Column<ReleaseSettings>(type: "jsonb", nullable: true),
                Files = table.Column<List<ReleaseFileChange>>(type: "jsonb", nullable: true),
                PackageIds = table.Column<string>(type: "jsonb", nullable: true),
                DispatchInputs = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: true),
                BaseSha = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Argument = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedByUserName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                DecidedByUserName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                DecisionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                StoreJobId = table.Column<long>(type: "bigint", nullable: true),
                CommitSha = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                RunId = table.Column<long>(type: "bigint", nullable: true),
                RunUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                RunStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                RunConclusion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                FollowStatus = table.Column<byte>(type: "smallint", nullable: true),
                StartedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                FinishedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RegistryPlans", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_RegistryConnections_OrganizationId_Kind",
            table: "RegistryConnections",
            columns: new[] { "OrganizationId", "Kind" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RegistryPlans_FollowStatus",
            table: "RegistryPlans",
            column: "FollowStatus");

        migrationBuilder.CreateIndex(
            name: "IX_RegistryPlans_OrganizationId_Status",
            table: "RegistryPlans",
            columns: new[] { "OrganizationId", "Status" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "RegistryConnections");

        migrationBuilder.DropTable(
            name: "RegistryPlans");

        migrationBuilder.DropColumn(
            name: "Release",
            table: "TrackedPackages");

        migrationBuilder.DropColumn(
            name: "Release",
            table: "PackageGroups");
    }
}
