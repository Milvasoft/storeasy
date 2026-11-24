using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Storeasy.Api.Migrations;

/// <inheritdoc />
public partial class _20261124200804_AddEasBuilds : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "EasBuilds",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                AppId = table.Column<int>(type: "integer", nullable: false),
                ProjectId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                BuildId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                ReceiptId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Platform = table.Column<byte>(type: "smallint", nullable: false),
                Status = table.Column<byte>(type: "smallint", nullable: false),
                Profile = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                GitRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                GitCommitHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                AppVersion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                AppBuildVersion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Distribution = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                StartedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                StartedInStoreasy = table.Column<bool>(type: "boolean", nullable: false),
                PageUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                LogsUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                ArtifactUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                StartedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                FinishedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ErrorCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                ArtifactSessionId = table.Column<long>(type: "bigint", nullable: true),
                EndReported = table.Column<bool>(type: "boolean", nullable: false),
                AfterBuild = table.Column<byte>(type: "smallint", nullable: false),
                PrepareReviewPlan = table.Column<bool>(type: "boolean", nullable: false),
                SubmitTrack = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                SubmitReleaseStatus = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                ChainDone = table.Column<bool>(type: "boolean", nullable: false),
                ReviewPlanDone = table.Column<bool>(type: "boolean", nullable: false),
                ReviewPlanIds = table.Column<string>(type: "jsonb", nullable: true),
                ChainError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EasBuilds", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "EasProjectLinks",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                AppId = table.Column<int>(type: "integer", nullable: false),
                ProjectId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                AccountName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                GitHubRepository = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                DefaultBranch = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                SyncedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                SyncError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                AfterBuild = table.Column<byte>(type: "smallint", nullable: false),
                PrepareReviewPlan = table.Column<bool>(type: "boolean", nullable: false),
                SubmitTrack = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                SubmitReleaseStatus = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EasProjectLinks", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "EasSubmissions",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                AppId = table.Column<int>(type: "integer", nullable: false),
                EasBuildId = table.Column<long>(type: "bigint", nullable: false),
                SubmissionId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Platform = table.Column<byte>(type: "smallint", nullable: false),
                Status = table.Column<byte>(type: "smallint", nullable: false),
                Track = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                ReleaseStatus = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                StartedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                FromChain = table.Column<bool>(type: "boolean", nullable: false),
                StartedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                FinishedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LogsUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                ErrorCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                EndReported = table.Column<bool>(type: "boolean", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EasSubmissions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ExpoConnections",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                Fingerprint = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                EncryptedSecret = table.Column<byte[]>(type: "bytea", nullable: true),
                EncryptedDataKey = table.Column<byte[]>(type: "bytea", nullable: true),
                KeyVersion = table.Column<int>(type: "integer", nullable: false),
                Status = table.Column<byte>(type: "smallint", nullable: false),
                LastVerifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastVerifyError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExpoConnections", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_EasBuilds_AppId_CreationDate",
            table: "EasBuilds",
            columns: new[] { "AppId", "CreationDate" });

        migrationBuilder.CreateIndex(
            name: "IX_EasBuilds_OrganizationId_BuildId",
            table: "EasBuilds",
            columns: new[] { "OrganizationId", "BuildId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_EasBuilds_Status",
            table: "EasBuilds",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_EasProjectLinks_AppId",
            table: "EasProjectLinks",
            column: "AppId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_EasProjectLinks_OrganizationId",
            table: "EasProjectLinks",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_EasSubmissions_AppId",
            table: "EasSubmissions",
            column: "AppId");

        migrationBuilder.CreateIndex(
            name: "IX_EasSubmissions_OrganizationId_SubmissionId",
            table: "EasSubmissions",
            columns: new[] { "OrganizationId", "SubmissionId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_EasSubmissions_Status",
            table: "EasSubmissions",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_ExpoConnections_OrganizationId",
            table: "ExpoConnections",
            column: "OrganizationId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "EasBuilds");

        migrationBuilder.DropTable(
            name: "EasProjectLinks");

        migrationBuilder.DropTable(
            name: "EasSubmissions");

        migrationBuilder.DropTable(
            name: "ExpoConnections");
    }
}
