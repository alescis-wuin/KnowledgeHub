using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Kh009AddWorkspacePresentationAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkspaceAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<int>(type: "integer", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, collation: "C"),
                    StorageKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, collation: "C"),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, collation: "C"),
                    Length = table.Column<long>(type: "bigint", nullable: false),
                    PixelWidth = table.Column<int>(type: "integer", nullable: false),
                    PixelHeight = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceAssets", x => x.Id);
                    table.UniqueConstraint("AK_WorkspaceAssets_WorkspaceId_Id", x => new { x.WorkspaceId, x.Id });
                    table.CheckConstraint("CK_WorkspaceAssets_ContentType", "\"ContentType\" IN ('image/png','image/jpeg','image/webp')");
                    table.CheckConstraint("CK_WorkspaceAssets_Dimensions", "\"PixelWidth\" BETWEEN 1 AND 8192 AND \"PixelHeight\" BETWEEN 1 AND 8192 AND \"PixelWidth\"::bigint * \"PixelHeight\"::bigint <= 16777216");
                    table.CheckConstraint("CK_WorkspaceAssets_Length", "\"Length\" >= 1 AND ((\"Purpose\" = 0 AND \"Length\" <= 5242880) OR (\"Purpose\" = 1 AND \"Length\" <= 10485760))");
                    table.CheckConstraint("CK_WorkspaceAssets_Purpose", "\"Purpose\" BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_WorkspaceAssets_Sha256", "\"Sha256\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("CK_WorkspaceAssets_StorageKey", "\"StorageKey\" ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "FK_WorkspaceAssets_AppUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkspaceAssets_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkspaceAuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceAuditEvents", x => x.Id);
                    table.CheckConstraint("CK_WorkspaceAuditEvents_EventType", "\"EventType\" BETWEEN 0 AND 15");
                    table.CheckConstraint("CK_WorkspaceAuditEvents_Outcome", "\"Outcome\" BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_WorkspaceAuditEvents_TargetIdNotEmpty", "\"TargetId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("CK_WorkspaceAuditEvents_TargetMatchesEvent", "((\"EventType\" IN (0,1,2,3,15) AND \"TargetType\" = 0) OR (\"EventType\" IN (4,5,6,7) AND \"TargetType\" = 1) OR (\"EventType\" IN (8,9,10) AND \"TargetType\" = 2) OR (\"EventType\" = 11 AND \"TargetType\" = 3) OR (\"EventType\" = 12 AND \"TargetType\" = 4) OR (\"EventType\" IN (13,14) AND \"TargetType\" = 5)) AND (\"TargetType\" NOT IN (0,3,4) OR \"TargetId\" = \"WorkspaceId\")");
                    table.CheckConstraint("CK_WorkspaceAuditEvents_TargetType", "\"TargetType\" BETWEEN 0 AND 5");
                    table.ForeignKey(
                        name: "FK_WorkspaceAuditEvents_AppUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkspaceAuditEvents_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkspaceHomepages",
                columns: table => new
                {
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    HeaderTitle = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Subtitle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    WelcomeMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceHomepages", x => x.WorkspaceId);
                    table.CheckConstraint("CK_WorkspaceHomepages_HeaderTitleNotBlank", "length(btrim(\"HeaderTitle\")) > 0");
                    table.CheckConstraint("CK_WorkspaceHomepages_VersionPositive", "\"Version\" >= 1");
                    table.ForeignKey(
                        name: "FK_WorkspaceHomepages_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkspaceBrandings",
                columns: table => new
                {
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    LogoAssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    BannerAssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    PrimaryColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false, collation: "C"),
                    SecondaryColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false, collation: "C"),
                    AccentColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false, collation: "C"),
                    BackgroundColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false, collation: "C"),
                    SurfaceColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false, collation: "C"),
                    TextColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false, collation: "C"),
                    TextOnPrimaryColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false, collation: "C"),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceBrandings", x => x.WorkspaceId);
                    table.CheckConstraint("CK_WorkspaceBrandings_AccentColor", "\"AccentColor\" ~ '^#[0-9A-F]{6}$'");
                    table.CheckConstraint("CK_WorkspaceBrandings_BackgroundColor", "\"BackgroundColor\" ~ '^#[0-9A-F]{6}$'");
                    table.CheckConstraint("CK_WorkspaceBrandings_DisplayNameNotBlank", "length(btrim(\"DisplayName\")) > 0");
                    table.CheckConstraint("CK_WorkspaceBrandings_DistinctAssets", "\"LogoAssetId\" IS NULL OR \"BannerAssetId\" IS NULL OR \"LogoAssetId\" <> \"BannerAssetId\"");
                    table.CheckConstraint("CK_WorkspaceBrandings_PrimaryColor", "\"PrimaryColor\" ~ '^#[0-9A-F]{6}$'");
                    table.CheckConstraint("CK_WorkspaceBrandings_SecondaryColor", "\"SecondaryColor\" ~ '^#[0-9A-F]{6}$'");
                    table.CheckConstraint("CK_WorkspaceBrandings_SurfaceColor", "\"SurfaceColor\" ~ '^#[0-9A-F]{6}$'");
                    table.CheckConstraint("CK_WorkspaceBrandings_TextColor", "\"TextColor\" ~ '^#[0-9A-F]{6}$'");
                    table.CheckConstraint("CK_WorkspaceBrandings_TextOnPrimaryColor", "\"TextOnPrimaryColor\" ~ '^#[0-9A-F]{6}$'");
                    table.CheckConstraint("CK_WorkspaceBrandings_VersionPositive", "\"Version\" >= 1");
                    table.ForeignKey(
                        name: "FK_WorkspaceBrandings_WorkspaceAssets_WorkspaceId_BannerAssetId",
                        columns: x => new { x.WorkspaceId, x.BannerAssetId },
                        principalTable: "WorkspaceAssets",
                        principalColumns: new[] { "WorkspaceId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkspaceBrandings_WorkspaceAssets_WorkspaceId_LogoAssetId",
                        columns: x => new { x.WorkspaceId, x.LogoAssetId },
                        principalTable: "WorkspaceAssets",
                        principalColumns: new[] { "WorkspaceId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkspaceBrandings_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkspaceHomeSections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    LinksJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceHomeSections", x => x.Id);
                    table.CheckConstraint("CK_WorkspaceHomeSections_ContentByType", "CASE WHEN jsonb_typeof(\"LinksJson\") <> 'array' THEN false ELSE (\"Type\" = 0 AND \"Body\" IS NOT NULL AND length(btrim(\"Body\")) > 0 AND jsonb_array_length(\"LinksJson\") = 0) OR (\"Type\" = 1 AND \"Body\" IS NULL AND jsonb_array_length(\"LinksJson\") BETWEEN 1 AND 10) OR (\"Type\" = 2 AND \"Body\" IS NULL AND jsonb_array_length(\"LinksJson\") = 0) END");
                    table.CheckConstraint("CK_WorkspaceHomeSections_LinksShape", "jsonb_typeof(\"LinksJson\") = 'array'");
                    table.CheckConstraint("CK_WorkspaceHomeSections_TitleNotBlank", "length(btrim(\"Title\")) > 0");
                    table.CheckConstraint("CK_WorkspaceHomeSections_ValidOrder", "\"SortOrder\" BETWEEN 0 AND 19");
                    table.CheckConstraint("CK_WorkspaceHomeSections_ValidType", "\"Type\" BETWEEN 0 AND 2");
                    table.ForeignKey(
                        name: "FK_WorkspaceHomeSections_WorkspaceHomepages_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "WorkspaceHomepages",
                        principalColumn: "WorkspaceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceAssets_CreatedByUserId",
                table: "WorkspaceAssets",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_WorkspaceAssets_WorkspaceStorageKey",
                table: "WorkspaceAssets",
                columns: new[] { "WorkspaceId", "StorageKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceAuditEvents_ActorUserId",
                table: "WorkspaceAuditEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceAuditEvents_WorkspaceOccurredId",
                table: "WorkspaceAuditEvents",
                columns: new[] { "WorkspaceId", "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceBrandings_WorkspaceId_BannerAssetId",
                table: "WorkspaceBrandings",
                columns: new[] { "WorkspaceId", "BannerAssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceBrandings_WorkspaceId_LogoAssetId",
                table: "WorkspaceBrandings",
                columns: new[] { "WorkspaceId", "LogoAssetId" });

            migrationBuilder.CreateIndex(
                name: "UX_WorkspaceHomeSections_WorkspaceOrder",
                table: "WorkspaceHomeSections",
                columns: new[] { "WorkspaceId", "SortOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkspaceAuditEvents");

            migrationBuilder.DropTable(
                name: "WorkspaceBrandings");

            migrationBuilder.DropTable(
                name: "WorkspaceHomeSections");

            migrationBuilder.DropTable(
                name: "WorkspaceAssets");

            migrationBuilder.DropTable(
                name: "WorkspaceHomepages");
        }
    }
}
