using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Storeasy.Domain.Intake;

#nullable disable

namespace Storeasy.Api.Migrations;

/// <inheritdoc />
public partial class _20261118154640_Phase3McpIntake : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ArtifactPullSources",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                AppId = table.Column<int>(type: "integer", nullable: false),
                Kind = table.Column<byte>(type: "smallint", nullable: false),
                PublicId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                EncryptedSecret = table.Column<byte[]>(type: "bytea", nullable: true),
                EncryptedDataKey = table.Column<byte[]>(type: "bytea", nullable: true),
                KeyVersion = table.Column<int>(type: "integer", nullable: false),
                AllowedDomains = table.Column<string>(type: "jsonb", nullable: true),
                Platforms = table.Column<string>(type: "jsonb", nullable: true),
                IncludeApk = table.Column<bool>(type: "boolean", nullable: false),
                SecretSetDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                SecretSetByUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                LastCallDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastCallResult = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ArtifactPullSources", x => x.Id);
                table.ForeignKey(
                    name: "FK_ArtifactPullSources_Apps_AppId",
                    column: x => x.AppId,
                    principalTable: "Apps",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ArtifactSessions",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                AppId = table.Column<int>(type: "integer", nullable: false),
                Platform = table.Column<byte>(type: "smallint", nullable: false),
                FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                Size = table.Column<long>(type: "bigint", nullable: false),
                Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Source = table.Column<byte>(type: "smallint", nullable: false),
                SourceRef = table.Column<ArtifactSourceRef>(type: "jsonb", nullable: true),
                Delivery = table.Column<ArtifactDeliveryOverride>(type: "jsonb", nullable: true),
                Status = table.Column<byte>(type: "smallint", nullable: false),
                StatusDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                StoreJobId = table.Column<long>(type: "bigint", nullable: true),
                OriginalSessionId = table.Column<long>(type: "bigint", nullable: true),
                ObjectKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                UploadId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                PartSize = table.Column<long>(type: "bigint", nullable: false),
                ResumeCount = table.Column<int>(type: "integer", nullable: false),
                PackageDeleted = table.Column<bool>(type: "boolean", nullable: false),
                Steps = table.Column<List<ArtifactStep>>(type: "jsonb", nullable: true),
                Package = table.Column<ArtifactPackageInfo>(type: "jsonb", nullable: true),
                Checks = table.Column<List<ArtifactCheck>>(type: "jsonb", nullable: true),
                AppliedRule = table.Column<ArtifactAppliedRule>(type: "jsonb", nullable: true),
                StoreAppId = table.Column<int>(type: "integer", nullable: true),
                IdentityKind = table.Column<byte>(type: "smallint", nullable: true),
                TrustedPublisherId = table.Column<int>(type: "integer", nullable: true),
                CiTokenId = table.Column<int>(type: "integer", nullable: true),
                CiTokenPrefix = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                UserId = table.Column<int>(type: "integer", nullable: true),
                UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                HoldReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CiExitCode = table.Column<int>(type: "integer", nullable: true),
                CiSummary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                StoreBuildId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                StoreUploadId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                PullSourceId = table.Column<int>(type: "integer", nullable: true),
                DownloadUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ArtifactSessions", x => x.Id);
                table.ForeignKey(
                    name: "FK_ArtifactSessions_Apps_AppId",
                    column: x => x.AppId,
                    principalTable: "Apps",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "CiTokens",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                Prefix = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                AllApps = table.Column<bool>(type: "boolean", nullable: false),
                AppIds = table.Column<string>(type: "jsonb", nullable: true),
                Scopes = table.Column<string>(type: "jsonb", nullable: true),
                ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastUseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastUseSource = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                RevokeDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                CreatedByUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CiTokens", x => x.Id);
                table.ForeignKey(
                    name: "FK_CiTokens_Organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "DeliveryRules",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                AppId = table.Column<int>(type: "integer", nullable: false),
                Platform = table.Column<byte>(type: "smallint", nullable: false),
                Enabled = table.Column<bool>(type: "boolean", nullable: false),
                Target = table.Column<byte>(type: "smallint", nullable: false),
                TargetId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                TargetName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                OnWarning = table.Column<byte>(type: "smallint", nullable: false),
                RolloutFraction = table.Column<double>(type: "double precision", nullable: true),
                UpdatedByUserId = table.Column<int>(type: "integer", nullable: true),
                UpdatedByUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DeliveryRules", x => x.Id);
                table.ForeignKey(
                    name: "FK_DeliveryRules_Apps_AppId",
                    column: x => x.AppId,
                    principalTable: "Apps",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "IntakeSettings",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                ArtifactRetentionDays = table.Column<int>(type: "integer", nullable: false),
                MaxArtifactBytes = table.Column<long>(type: "bigint", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IntakeSettings", x => x.Id);
                table.ForeignKey(
                    name: "FK_IntakeSettings_Organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PersonalAccessTokens",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<int>(type: "integer", nullable: false),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                Prefix = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                ReadOnly = table.Column<bool>(type: "boolean", nullable: false),
                ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastUseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastClientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                RevokeDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PersonalAccessTokens", x => x.Id);
                table.ForeignKey(
                    name: "FK_PersonalAccessTokens_Organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PersonalAccessTokens_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "TrustedPublishers",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                AppId = table.Column<int>(type: "integer", nullable: false),
                Provider = table.Column<byte>(type: "smallint", nullable: false),
                Repository = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Workflow = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                Branch = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Environment = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                AzureOrganization = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                AzureProject = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                ServiceConnection = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Issuer = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                LastExchangeDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastExchangeResult = table.Column<byte>(type: "smallint", nullable: true),
                LastExchangeRunUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                LastClaims = table.Column<List<OidcClaim>>(type: "jsonb", nullable: true),
                CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                CreatedByUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TrustedPublishers", x => x.Id);
                table.ForeignKey(
                    name: "FK_TrustedPublishers_Apps_AppId",
                    column: x => x.AppId,
                    principalTable: "Apps",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "UploadTokens",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                AppId = table.Column<int>(type: "integer", nullable: false),
                TrustedPublisherId = table.Column<int>(type: "integer", nullable: false),
                TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                Prefix = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                RunUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UploadTokens", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ArtifactPullSources_AppId",
            table: "ArtifactPullSources",
            column: "AppId");

        migrationBuilder.CreateIndex(
            name: "IX_ArtifactPullSources_OrganizationId",
            table: "ArtifactPullSources",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_ArtifactPullSources_PublicId",
            table: "ArtifactPullSources",
            column: "PublicId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ArtifactSessions_AppId",
            table: "ArtifactSessions",
            column: "AppId");

        migrationBuilder.CreateIndex(
            name: "IX_ArtifactSessions_OrganizationId_AppId_Sha256",
            table: "ArtifactSessions",
            columns: new[] { "OrganizationId", "AppId", "Sha256" });

        migrationBuilder.CreateIndex(
            name: "IX_ArtifactSessions_OrganizationId_Status",
            table: "ArtifactSessions",
            columns: new[] { "OrganizationId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_ArtifactSessions_Status_StartDate",
            table: "ArtifactSessions",
            columns: new[] { "Status", "StartDate" });

        migrationBuilder.CreateIndex(
            name: "IX_CiTokens_OrganizationId",
            table: "CiTokens",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_CiTokens_TokenHash",
            table: "CiTokens",
            column: "TokenHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_DeliveryRules_AppId_Platform",
            table: "DeliveryRules",
            columns: new[] { "AppId", "Platform" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_DeliveryRules_OrganizationId",
            table: "DeliveryRules",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_IntakeSettings_OrganizationId",
            table: "IntakeSettings",
            column: "OrganizationId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PersonalAccessTokens_OrganizationId",
            table: "PersonalAccessTokens",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_PersonalAccessTokens_TokenHash",
            table: "PersonalAccessTokens",
            column: "TokenHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PersonalAccessTokens_UserId_OrganizationId",
            table: "PersonalAccessTokens",
            columns: new[] { "UserId", "OrganizationId" });

        migrationBuilder.CreateIndex(
            name: "IX_TrustedPublishers_AppId",
            table: "TrustedPublishers",
            column: "AppId");

        migrationBuilder.CreateIndex(
            name: "IX_TrustedPublishers_OrganizationId",
            table: "TrustedPublishers",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_UploadTokens_ExpiryDate",
            table: "UploadTokens",
            column: "ExpiryDate");

        migrationBuilder.CreateIndex(
            name: "IX_UploadTokens_TokenHash",
            table: "UploadTokens",
            column: "TokenHash",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ArtifactPullSources");

        migrationBuilder.DropTable(
            name: "ArtifactSessions");

        migrationBuilder.DropTable(
            name: "CiTokens");

        migrationBuilder.DropTable(
            name: "DeliveryRules");

        migrationBuilder.DropTable(
            name: "IntakeSettings");

        migrationBuilder.DropTable(
            name: "PersonalAccessTokens");

        migrationBuilder.DropTable(
            name: "TrustedPublishers");

        migrationBuilder.DropTable(
            name: "UploadTokens");
    }
}
