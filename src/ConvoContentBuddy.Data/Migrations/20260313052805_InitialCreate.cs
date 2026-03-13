using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace ConvoContentBuddy.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "app");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:uuid-ossp", ",,")
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "ingestion_snapshots",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    source = table.Column<string>(type: "text", nullable: false),
                    captured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    problem_count = table.Column<int>(type: "integer", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    is_latest = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingestion_snapshots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "problems",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    slug = table.Column<string>(type: "text", nullable: false),
                    question_id = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    difficulty = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    embedding = table.Column<Vector>(type: "vector(1536)", nullable: true),
                    embedding_model = table.Column<string>(type: "text", nullable: true),
                    embedding_dimensions = table.Column<int>(type: "integer", nullable: true),
                    embedding_generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    seeded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_problems", x => x.id);
                    table.CheckConstraint("CK_problems_difficulty", "difficulty IN ('Easy', 'Medium', 'Hard')");
                });

            migrationBuilder.CreateTable(
                name: "tags",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tags", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "problem_tags",
                schema: "app",
                columns: table => new
                {
                    problem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_problem_tags", x => new { x.problem_id, x.tag_id });
                    table.ForeignKey(
                        name: "FK_problem_tags_problems_problem_id",
                        column: x => x.problem_id,
                        principalSchema: "app",
                        principalTable: "problems",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_problem_tags_tags_tag_id",
                        column: x => x.tag_id,
                        principalSchema: "app",
                        principalTable: "tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ingestion_snapshots_captured_at",
                schema: "app",
                table: "ingestion_snapshots",
                column: "captured_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_problem_tags_tag_id",
                schema: "app",
                table: "problem_tags",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "IX_problems_embedding",
                schema: "app",
                table: "problems",
                column: "embedding",
                filter: "embedding IS NOT NULL")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_problems_embedding_model_embedding_dimensions",
                schema: "app",
                table: "problems",
                columns: new[] { "embedding_model", "embedding_dimensions" });

            migrationBuilder.CreateIndex(
                name: "IX_problems_question_id",
                schema: "app",
                table: "problems",
                column: "question_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_problems_slug",
                schema: "app",
                table: "problems",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tags_name",
                schema: "app",
                table: "tags",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ingestion_snapshots",
                schema: "app");

            migrationBuilder.DropTable(
                name: "problem_tags",
                schema: "app");

            migrationBuilder.DropTable(
                name: "problems",
                schema: "app");

            migrationBuilder.DropTable(
                name: "tags",
                schema: "app");
        }
    }
}
