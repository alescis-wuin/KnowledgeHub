using KnowledgeHub.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Infrastructure.Migrations;

[DbContext(typeof(KnowledgeDbContext))]
[Migration("20261031130000_AddExternalDocumentIdentity")]
public partial class AddExternalDocumentIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "ExternalId",
            table: "Documents",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ContentSha256",
            table: "Documents",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        // PostgreSQL accepts multiple NULLs in a UNIQUE index. Pre-existing
        // documents remain unclaimed and do not receive synthetic IDs.
        migrationBuilder.CreateIndex(
            name: "IX_Documents_ExternalId",
            table: "Documents",
            column: "ExternalId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Documents_ExternalId",
            table: "Documents");
        migrationBuilder.DropColumn(name: "ContentSha256", table: "Documents");
        migrationBuilder.DropColumn(name: "ExternalId", table: "Documents");
    }
}
