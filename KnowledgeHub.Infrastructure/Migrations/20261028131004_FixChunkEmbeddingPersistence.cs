using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixChunkEmbeddingPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ChunkEmbeddings_ChunkId",
                table: "ChunkEmbeddings",
                column: "ChunkId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChunkEmbeddings_DocumentChunks_ChunkId",
                table: "ChunkEmbeddings",
                column: "ChunkId",
                principalTable: "DocumentChunks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChunkEmbeddings_DocumentChunks_ChunkId",
                table: "ChunkEmbeddings");

            migrationBuilder.DropIndex(
                name: "IX_ChunkEmbeddings_ChunkId",
                table: "ChunkEmbeddings");
        }
    }
}
