using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluationDatasets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChunkEmbeddings_ChunkId",
                table: "ChunkEmbeddings");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "EvaluationDatasets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationDatasets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EvaluationQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DatasetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Question = table.Column<string>(type: "text", nullable: false),
                    ExpectedAnswer = table.Column<string>(type: "text", nullable: true),
                    Difficulty = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluationQuestions_EvaluationDatasets_DatasetId",
                        column: x => x.DatasetId,
                        principalTable: "EvaluationDatasets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EvaluationReferenceDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EvaluationQuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalDocumentId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Title = table.Column<string>(type: "text", nullable: true),
                    Content = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationReferenceDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluationReferenceDocuments_EvaluationQuestions_Evaluation~",
                        column: x => x.EvaluationQuestionId,
                        principalTable: "EvaluationQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChunkEmbeddings_ChunkId",
                table: "ChunkEmbeddings",
                column: "ChunkId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationQuestions_DatasetId",
                table: "EvaluationQuestions",
                column: "DatasetId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationReferenceDocuments_EvaluationQuestionId",
                table: "EvaluationReferenceDocuments",
                column: "EvaluationQuestionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EvaluationReferenceDocuments");

            migrationBuilder.DropTable(
                name: "EvaluationQuestions");

            migrationBuilder.DropTable(
                name: "EvaluationDatasets");

            migrationBuilder.DropIndex(
                name: "IX_ChunkEmbeddings_ChunkId",
                table: "ChunkEmbeddings");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateIndex(
                name: "IX_ChunkEmbeddings_ChunkId",
                table: "ChunkEmbeddings",
                column: "ChunkId");
        }
    }
}
