using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Kh009AddWorkspaceIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Issuer = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false, collation: "C"),
                    Subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false, collation: "C"),
                    DisplayName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    EmailNormalized = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    EmailVerified = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUsers", x => x.Id);
                    table.CheckConstraint("CK_AppUsers_DisplayNameNotBlank", "length(btrim(\"DisplayName\")) > 0");
                    table.CheckConstraint("CK_AppUsers_IdentityNotBlank", "length(btrim(\"Issuer\")) > 0 AND length(btrim(\"Subject\")) > 0");
                    table.CheckConstraint("CK_AppUsers_VerifiedEmail", "NOT \"EmailVerified\" OR \"EmailNormalized\" IS NOT NULL");
                });

            migrationBuilder.CreateTable(
                name: "Workspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    SlugNormalized = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false, collation: "C"),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsVerifiedOrganization = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspaces", x => x.Id);
                    table.CheckConstraint("CK_Workspaces_ArchivedAfterCreated", "\"ArchivedAtUtc\" IS NULL OR \"ArchivedAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_Workspaces_NameNotBlank", "length(btrim(\"Name\")) > 0");
                    table.CheckConstraint("CK_Workspaces_SlugFormat", "length(\"SlugNormalized\") BETWEEN 3 AND 80 AND \"SlugNormalized\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
                    table.CheckConstraint("CK_Workspaces_VersionPositive", "\"Version\" >= 1");
                    table.ForeignKey(
                        name: "FK_Workspaces_AppUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkspaceInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    InviterUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailNormalized = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false, collation: "C"),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, collation: "C"),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcceptedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceInvitations", x => x.Id);
                    table.CheckConstraint("CK_WorkspaceInvitations_AcceptedByConsistency", "(\"AcceptedAtUtc\" IS NULL) = (\"AcceptedByUserId\" IS NULL)");
                    table.CheckConstraint("CK_WorkspaceInvitations_EmailNotBlank", "length(btrim(\"EmailNormalized\")) > 3 AND position('@' in \"EmailNormalized\") > 1");
                    table.CheckConstraint("CK_WorkspaceInvitations_ExpiresLater", "\"ExpiresAtUtc\" > \"CreatedAtUtc\" AND \"ExpiresAtUtc\" <= \"CreatedAtUtc\" + INTERVAL '14 days'");
                    table.CheckConstraint("CK_WorkspaceInvitations_NotAcceptedAndRevoked", "\"AcceptedAtUtc\" IS NULL OR \"RevokedAtUtc\" IS NULL");
                    table.CheckConstraint("CK_WorkspaceInvitations_RoleNoOwner", "\"Role\" BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_WorkspaceInvitations_TokenHash", "\"TokenHash\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("CK_WorkspaceInvitations_VersionPositive", "\"Version\" >= 1");
                    table.ForeignKey(
                        name: "FK_WorkspaceInvitations_AppUsers_AcceptedByUserId",
                        column: x => x.AcceptedByUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkspaceInvitations_AppUsers_InviterUserId",
                        column: x => x.InviterUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkspaceInvitations_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkspaceMembers",
                columns: table => new
                {
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    JoinedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceMembers", x => new { x.WorkspaceId, x.UserId });
                    table.CheckConstraint("CK_WorkspaceMembers_OwnerMustBeActive", "\"Role\" <> 4 OR \"Status\" = 1");
                    table.CheckConstraint("CK_WorkspaceMembers_ValidRole", "\"Role\" BETWEEN 0 AND 4");
                    table.CheckConstraint("CK_WorkspaceMembers_ValidStatus", "\"Status\" BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_WorkspaceMembers_VersionPositive", "\"Version\" >= 1");
                    table.ForeignKey(
                        name: "FK_WorkspaceMembers_AppUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkspaceMembers_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_AppUsers_OidcIdentity",
                table: "AppUsers",
                columns: new[] { "Issuer", "Subject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceInvitations_AcceptedByUserId",
                table: "WorkspaceInvitations",
                column: "AcceptedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceInvitations_InviterUserId",
                table: "WorkspaceInvitations",
                column: "InviterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceInvitations_WorkspaceId_EmailNormalized",
                table: "WorkspaceInvitations",
                columns: new[] { "WorkspaceId", "EmailNormalized" });

            migrationBuilder.CreateIndex(
                name: "UX_WorkspaceInvitations_TokenHash",
                table: "WorkspaceInvitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceMembers_UserId",
                table: "WorkspaceMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_CreatedByUserId",
                table: "Workspaces",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "UX_Workspaces_SlugNormalized",
                table: "Workspaces",
                column: "SlugNormalized",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkspaceInvitations");

            migrationBuilder.DropTable(
                name: "WorkspaceMembers");

            migrationBuilder.DropTable(
                name: "Workspaces");

            migrationBuilder.DropTable(
                name: "AppUsers");
        }
    }
}
