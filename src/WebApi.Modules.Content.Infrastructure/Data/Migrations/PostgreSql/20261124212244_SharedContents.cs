using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace soft.square.net.WebApi.Modules.Content.Infrastructure.Data.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class SharedContents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ContentsSplit",
                schema: "content",
                table: "articles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "contents",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NodeId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FieldKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FieldVersion = table.Column<int>(type: "integer", nullable: false),
                    PostKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ValuesJson = table.Column<string>(type: "text", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    PressId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CategoryId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OriginPostId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    SectionId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AssigneeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Stage = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AcceptedRevision = table.Column<int>(type: "integer", nullable: true),
                    ReviewNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "events",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PostId = table.Column<int>(type: "integer", nullable: true),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: true),
                    Stage = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    PostState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    By = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "pipelines",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    DefinitionJson = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pipelines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "post_contents",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PostId = table.Column<int>(type: "integer", nullable: false),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Required = table.Column<bool>(type: "boolean", nullable: false),
                    PinnedRevision = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_contents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_post_contents_articles_PostId",
                        column: x => x.PostId,
                        principalSchema: "content",
                        principalTable: "articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "post_slugs",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PostId = table.Column<int>(type: "integer", nullable: false),
                    Culture = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_slugs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_post_slugs_articles_PostId",
                        column: x => x.PostId,
                        principalSchema: "content",
                        principalTable: "articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "content_history",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Actor = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Stage = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Detail = table.Column<string>(type: "character varying(1100)", maxLength: 1100, nullable: true),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_content_history_contents_ContentId",
                        column: x => x.ContentId,
                        principalSchema: "content",
                        principalTable: "contents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "content_revisions",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    FieldKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FieldVersion = table.Column<int>(type: "integer", nullable: false),
                    ValuesJson = table.Column<string>(type: "text", nullable: false),
                    By = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_revisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_content_revisions_contents_ContentId",
                        column: x => x.ContentId,
                        principalSchema: "content",
                        principalTable: "contents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_content_history_ContentId",
                schema: "content",
                table: "content_history",
                column: "ContentId");

            migrationBuilder.CreateIndex(
                name: "IX_content_revisions_ContentId_Revision",
                schema: "content",
                table: "content_revisions",
                columns: new[] { "ContentId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contents_AssigneeId",
                schema: "content",
                table: "contents",
                column: "AssigneeId");

            migrationBuilder.CreateIndex(
                name: "IX_contents_CategoryId",
                schema: "content",
                table: "contents",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_contents_Stage_SectionId",
                schema: "content",
                table: "contents",
                columns: new[] { "Stage", "SectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_contents_TenantId",
                schema: "content",
                table: "contents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_contents_UpdatedAt",
                schema: "content",
                table: "contents",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_events_At",
                schema: "content",
                table: "events",
                column: "At");

            migrationBuilder.CreateIndex(
                name: "IX_events_ContentId",
                schema: "content",
                table: "events",
                column: "ContentId");

            migrationBuilder.CreateIndex(
                name: "IX_events_PostId",
                schema: "content",
                table: "events",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_events_TenantId",
                schema: "content",
                table: "events",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_pipelines_TenantId",
                schema: "content",
                table: "pipelines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_post_contents_ContentId",
                schema: "content",
                table: "post_contents",
                column: "ContentId");

            migrationBuilder.CreateIndex(
                name: "IX_post_contents_PostId_ContentId",
                schema: "content",
                table: "post_contents",
                columns: new[] { "PostId", "ContentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_post_slugs_Culture_Slug",
                schema: "content",
                table: "post_slugs",
                columns: new[] { "Culture", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_post_slugs_PostId_Culture",
                schema: "content",
                table: "post_slugs",
                columns: new[] { "PostId", "Culture" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "content_history",
                schema: "content");

            migrationBuilder.DropTable(
                name: "content_revisions",
                schema: "content");

            migrationBuilder.DropTable(
                name: "events",
                schema: "content");

            migrationBuilder.DropTable(
                name: "pipelines",
                schema: "content");

            migrationBuilder.DropTable(
                name: "post_contents",
                schema: "content");

            migrationBuilder.DropTable(
                name: "post_slugs",
                schema: "content");

            migrationBuilder.DropTable(
                name: "contents",
                schema: "content");

            migrationBuilder.DropColumn(
                name: "ContentsSplit",
                schema: "content",
                table: "articles");
        }
    }
}
