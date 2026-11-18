using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Storeasy.Domain.JsonModels;

#nullable disable

namespace Storeasy.Api.Migrations;

/// <inheritdoc />
public partial class _20261118144228_Phase4Events : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "OrganizationId",
            table: "InternalNotifications",
            type: "integer",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "AppVersionStates",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                AppId = table.Column<int>(type: "integer", nullable: false),
                StoreAppId = table.Column<int>(type: "integer", nullable: false),
                Store = table.Column<byte>(type: "smallint", nullable: false),
                ExternalVersionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                VersionString = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                BuildNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                State = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                PreviousState = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                ChangedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                SubmittedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AppVersionStates", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "BetaAppLocalizations",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                StoreAppId = table.Column<int>(type: "integer", nullable: false),
                ExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Locale = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                FeedbackEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                MarketingUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                PrivacyPolicyUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                SyncedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BetaAppLocalizations", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "BetaBuilds",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                StoreAppId = table.Column<int>(type: "integer", nullable: false),
                BuildId = table.Column<long>(type: "bigint", nullable: false),
                BetaReviewState = table.Column<byte>(type: "smallint", nullable: false),
                BetaReviewSubmittedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                WhatToTest = table.Column<List<LocalizedText>>(type: "jsonb", nullable: true),
                SyncedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BetaBuilds", x => x.Id);
                table.ForeignKey(
                    name: "FK_BetaBuilds_Builds_BuildId",
                    column: x => x.BuildId,
                    principalTable: "Builds",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "BetaGroups",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                StoreAppId = table.Column<int>(type: "integer", nullable: false),
                ExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                IsInternal = table.Column<bool>(type: "boolean", nullable: false),
                PublicLinkEnabled = table.Column<bool>(type: "boolean", nullable: false),
                PublicLink = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                PublicLinkLimit = table.Column<int>(type: "integer", nullable: true),
                PublicLinkLimitEnabled = table.Column<bool>(type: "boolean", nullable: false),
                TesterCount = table.Column<int>(type: "integer", nullable: false),
                BuildExternalIds = table.Column<string>(type: "jsonb", nullable: true),
                SyncedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BetaGroups", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "BetaTesters",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                StoreAppId = table.Column<int>(type: "integer", nullable: false),
                ExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                FirstName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                LastName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                InviteType = table.Column<byte>(type: "smallint", nullable: false),
                State = table.Column<byte>(type: "smallint", nullable: false),
                GroupKeys = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                SyncedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BetaTesters", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "DeadlineReminders",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                Subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                ThresholdDays = table.Column<int>(type: "integer", nullable: false),
                SentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DeadlineReminders", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "NotificationPreferences",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<int>(type: "integer", nullable: false),
                Kind = table.Column<byte>(type: "smallint", nullable: false),
                InApp = table.Column<bool>(type: "boolean", nullable: false),
                Email = table.Column<bool>(type: "boolean", nullable: false),
                WebPush = table.Column<bool>(type: "boolean", nullable: false),
                UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NotificationPreferences", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "OrganizationWebhooks",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                Events = table.Column<string>(type: "jsonb", nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                EncryptedSecret = table.Column<byte[]>(type: "bytea", nullable: true),
                EncryptedDataKey = table.Column<byte[]>(type: "bytea", nullable: true),
                KeyVersion = table.Column<int>(type: "integer", nullable: false),
                LastDeliveryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastDeliveryStatus = table.Column<byte>(type: "smallint", nullable: true),
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
                table.PrimaryKey("PK_OrganizationWebhooks", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "RejectionCases",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                AppId = table.Column<int>(type: "integer", nullable: false),
                StoreAppId = table.Column<int>(type: "integer", nullable: false),
                Store = table.Column<byte>(type: "smallint", nullable: false),
                ExternalVersionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                VersionString = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                BuildNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Kind = table.Column<byte>(type: "smallint", nullable: false),
                State = table.Column<byte>(type: "smallint", nullable: false),
                OpenedVia = table.Column<byte>(type: "smallint", nullable: false),
                OpenedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ClosedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ClosedVia = table.Column<byte>(type: "smallint", nullable: true),
                CloseNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                RawText = table.Column<string>(type: "character varying(50000)", maxLength: 50000, nullable: true),
                RawTextByUserName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                RawTextDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Guidelines = table.Column<List<RejectionGuideline>>(type: "jsonb", nullable: true),
                Checklist = table.Column<List<RejectionChecklistItem>>(type: "jsonb", nullable: true),
                ReplyDraft = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                ReplyDraftByUserName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                ReplyDraftChannel = table.Column<byte>(type: "smallint", nullable: true),
                ReplyDraftDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ReplySentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ReplySentByUserName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                PreviousCaseId = table.Column<int>(type: "integer", nullable: true),
                Timeline = table.Column<List<RejectionTimelineEntry>>(type: "jsonb", nullable: true),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatorUserName = table.Column<string>(type: "text", nullable: true),
                LastModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastModifierUserName = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RejectionCases", x => x.Id);
                table.ForeignKey(
                    name: "FK_RejectionCases_Apps_AppId",
                    column: x => x.AppId,
                    principalTable: "Apps",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "RuleProposals",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                Store = table.Column<byte>(type: "smallint", nullable: false),
                RuleSourceId = table.Column<int>(type: "integer", nullable: true),
                ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                PreviousContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                Status = table.Column<byte>(type: "smallint", nullable: false),
                PreparedBy = table.Column<byte>(type: "smallint", nullable: false),
                PreparedByUserName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                RuleIds = table.Column<string>(type: "jsonb", nullable: true),
                SourceExcerptBefore = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                SourceExcerptAfter = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                CatalogPatch = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                PreparerNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                CatalogVersionFrom = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                CatalogVersionTo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                RejectReason = table.Column<byte>(type: "smallint", nullable: true),
                DecisionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                DecidedByUserName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                DecidedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                NotifiedOrganizationCount = table.Column<int>(type: "integer", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RuleProposals", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "RuleSources",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Store = table.Column<byte>(type: "smallint", nullable: false),
                Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                Status = table.Column<byte>(type: "smallint", nullable: false),
                LastFetchDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastChangeDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                PreviousContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                Content = table.Column<string>(type: "character varying(200000)", maxLength: 200000, nullable: true),
                LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RuleSources", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "SavedReplies",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Variants = table.Column<List<LocalizedText>>(type: "jsonb", nullable: true),
                AppIds = table.Column<string>(type: "jsonb", nullable: true),
                UseCount = table.Column<int>(type: "integer", nullable: false),
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
                table.PrimaryKey("PK_SavedReplies", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "StoreReviews",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                AppId = table.Column<int>(type: "integer", nullable: false),
                StoreAppId = table.Column<int>(type: "integer", nullable: false),
                Store = table.Column<byte>(type: "smallint", nullable: false),
                ExternalReviewId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                AuthorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Rating = table.Column<int>(type: "integer", nullable: false),
                Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Body = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                Locale = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                Territory = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                AppVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                Device = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                ReviewDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ReplyBody = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                ReplyState = table.Column<byte>(type: "smallint", nullable: false),
                ReplyExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                ReplyDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ReplyByUserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                ReplyError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                ReplyJobId = table.Column<long>(type: "bigint", nullable: true),
                DraftBody = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                FetchedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StoreReviews", x => x.Id);
                table.ForeignKey(
                    name: "FK_StoreReviews_Apps_AppId",
                    column: x => x.AppId,
                    principalTable: "Apps",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "WebhookDeliveries",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                OrganizationWebhookId = table.Column<int>(type: "integer", nullable: false),
                EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                EventId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Payload = table.Column<string>(type: "jsonb", nullable: true),
                Status = table.Column<byte>(type: "smallint", nullable: false),
                Attempt = table.Column<int>(type: "integer", nullable: false),
                MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                NextAttemptDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LockedUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ResponseStatusCode = table.Column<int>(type: "integer", nullable: true),
                ResponseBody = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                Error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                DurationMilliseconds = table.Column<int>(type: "integer", nullable: true),
                IsTest = table.Column<bool>(type: "boolean", nullable: false),
                CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastAttemptDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LeaseId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WebhookDeliveries", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "WebhookEndpoints",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                StoreConnectionId = table.Column<int>(type: "integer", nullable: true),
                Source = table.Column<byte>(type: "smallint", nullable: false),
                PublicId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                EncryptedSecret = table.Column<byte[]>(type: "bytea", nullable: true),
                EncryptedDataKey = table.Column<byte[]>(type: "bytea", nullable: true),
                KeyVersion = table.Column<int>(type: "integer", nullable: false),
                PreviousEncryptedSecret = table.Column<byte[]>(type: "bytea", nullable: true),
                PreviousEncryptedDataKey = table.Column<byte[]>(type: "bytea", nullable: true),
                PreviousKeyVersion = table.Column<int>(type: "integer", nullable: true),
                PreviousSecretValidUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                SecretSetDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Registration = table.Column<byte>(type: "smallint", nullable: false),
                RegistrationError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                ExternalWebhooks = table.Column<List<ExternalWebhookRegistration>>(type: "jsonb", nullable: true),
                Events = table.Column<string>(type: "jsonb", nullable: true),
                LastEventDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                table.PrimaryKey("PK_WebhookEndpoints", x => x.Id);
                table.ForeignKey(
                    name: "FK_WebhookEndpoints_StoreConnections_StoreConnectionId",
                    column: x => x.StoreConnectionId,
                    principalTable: "StoreConnections",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "WebhookEvents",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OrganizationId = table.Column<int>(type: "integer", nullable: false),
                WebhookEndpointId = table.Column<int>(type: "integer", nullable: false),
                EventType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                ExternalEventId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Payload = table.Column<string>(type: "jsonb", nullable: true),
                SignatureHeaderMasked = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                SignatureValid = table.Column<bool>(type: "boolean", nullable: false),
                ReceivedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ProcessedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ProcessResult = table.Column<byte>(type: "smallint", nullable: false),
                ProcessMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                SubjectExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                StoreAppId = table.Column<int>(type: "integer", nullable: true),
                AppId = table.Column<int>(type: "integer", nullable: true),
                OldValue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                NewValue = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                RelatedEntityType = table.Column<byte>(type: "smallint", nullable: false),
                RelatedEntityId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WebhookEvents", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ReviewReads",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<int>(type: "integer", nullable: false),
                StoreReviewId = table.Column<long>(type: "bigint", nullable: false),
                ReadDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReviewReads", x => x.Id);
                table.ForeignKey(
                    name: "FK_ReviewReads_StoreReviews_StoreReviewId",
                    column: x => x.StoreReviewId,
                    principalTable: "StoreReviews",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AppVersionStates_OrganizationId",
            table: "AppVersionStates",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_AppVersionStates_StoreAppId_ExternalVersionId",
            table: "AppVersionStates",
            columns: new[] { "StoreAppId", "ExternalVersionId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_BetaAppLocalizations_OrganizationId",
            table: "BetaAppLocalizations",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_BetaAppLocalizations_StoreAppId_Locale",
            table: "BetaAppLocalizations",
            columns: new[] { "StoreAppId", "Locale" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_BetaBuilds_BuildId",
            table: "BetaBuilds",
            column: "BuildId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_BetaBuilds_OrganizationId",
            table: "BetaBuilds",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_BetaGroups_OrganizationId",
            table: "BetaGroups",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_BetaGroups_StoreAppId_ExternalId",
            table: "BetaGroups",
            columns: new[] { "StoreAppId", "ExternalId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_BetaTesters_OrganizationId",
            table: "BetaTesters",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_BetaTesters_StoreAppId_ExternalId",
            table: "BetaTesters",
            columns: new[] { "StoreAppId", "ExternalId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_DeadlineReminders_Subject_ThresholdDays",
            table: "DeadlineReminders",
            columns: new[] { "Subject", "ThresholdDays" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_NotificationPreferences_UserId_Kind",
            table: "NotificationPreferences",
            columns: new[] { "UserId", "Kind" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_OrganizationWebhooks_OrganizationId",
            table: "OrganizationWebhooks",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_RejectionCases_AppId",
            table: "RejectionCases",
            column: "AppId");

        migrationBuilder.CreateIndex(
            name: "IX_RejectionCases_OrganizationId_AppId",
            table: "RejectionCases",
            columns: new[] { "OrganizationId", "AppId" });

        migrationBuilder.CreateIndex(
            name: "IX_RejectionCases_StoreAppId_State",
            table: "RejectionCases",
            columns: new[] { "StoreAppId", "State" });

        migrationBuilder.CreateIndex(
            name: "IX_ReviewReads_StoreReviewId",
            table: "ReviewReads",
            column: "StoreReviewId");

        migrationBuilder.CreateIndex(
            name: "IX_ReviewReads_UserId_StoreReviewId",
            table: "ReviewReads",
            columns: new[] { "UserId", "StoreReviewId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RuleProposals_RuleSourceId_ContentHash",
            table: "RuleProposals",
            columns: new[] { "RuleSourceId", "ContentHash" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RuleProposals_Status",
            table: "RuleProposals",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_RuleSources_Url",
            table: "RuleSources",
            column: "Url",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SavedReplies_OrganizationId",
            table: "SavedReplies",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_StoreReviews_AppId",
            table: "StoreReviews",
            column: "AppId");

        migrationBuilder.CreateIndex(
            name: "IX_StoreReviews_OrganizationId_ReviewDate",
            table: "StoreReviews",
            columns: new[] { "OrganizationId", "ReviewDate" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "IX_StoreReviews_StoreAppId_ExternalReviewId",
            table: "StoreReviews",
            columns: new[] { "StoreAppId", "ExternalReviewId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_WebhookDeliveries_OrganizationWebhookId_CreationDate",
            table: "WebhookDeliveries",
            columns: new[] { "OrganizationWebhookId", "CreationDate" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "IX_WebhookDeliveries_Status_NextAttemptDate",
            table: "WebhookDeliveries",
            columns: new[] { "Status", "NextAttemptDate" });

        migrationBuilder.CreateIndex(
            name: "IX_WebhookEndpoints_OrganizationId",
            table: "WebhookEndpoints",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_WebhookEndpoints_PublicId",
            table: "WebhookEndpoints",
            column: "PublicId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_WebhookEndpoints_StoreConnectionId",
            table: "WebhookEndpoints",
            column: "StoreConnectionId");

        migrationBuilder.CreateIndex(
            name: "IX_WebhookEvents_OrganizationId_ReceivedDate",
            table: "WebhookEvents",
            columns: new[] { "OrganizationId", "ReceivedDate" },
            descending: new[] { false, true });

        migrationBuilder.CreateIndex(
            name: "IX_WebhookEvents_WebhookEndpointId_ExternalEventId",
            table: "WebhookEvents",
            columns: new[] { "WebhookEndpointId", "ExternalEventId" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AppVersionStates");

        migrationBuilder.DropTable(
            name: "BetaAppLocalizations");

        migrationBuilder.DropTable(
            name: "BetaBuilds");

        migrationBuilder.DropTable(
            name: "BetaGroups");

        migrationBuilder.DropTable(
            name: "BetaTesters");

        migrationBuilder.DropTable(
            name: "DeadlineReminders");

        migrationBuilder.DropTable(
            name: "NotificationPreferences");

        migrationBuilder.DropTable(
            name: "OrganizationWebhooks");

        migrationBuilder.DropTable(
            name: "RejectionCases");

        migrationBuilder.DropTable(
            name: "ReviewReads");

        migrationBuilder.DropTable(
            name: "RuleProposals");

        migrationBuilder.DropTable(
            name: "RuleSources");

        migrationBuilder.DropTable(
            name: "SavedReplies");

        migrationBuilder.DropTable(
            name: "WebhookDeliveries");

        migrationBuilder.DropTable(
            name: "WebhookEndpoints");

        migrationBuilder.DropTable(
            name: "WebhookEvents");

        migrationBuilder.DropTable(
            name: "StoreReviews");

        migrationBuilder.DropColumn(
            name: "OrganizationId",
            table: "InternalNotifications");
    }
}
