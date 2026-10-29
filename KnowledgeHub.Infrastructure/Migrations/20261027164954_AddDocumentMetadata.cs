using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SearchKeywords",
                table: "Documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "Documents",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SearchKeywords",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "Documents");
        }
    }
}
