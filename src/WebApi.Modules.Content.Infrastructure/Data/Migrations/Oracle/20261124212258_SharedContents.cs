using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace soft.square.net.WebApi.Modules.Content.Infrastructure.Data.Migrations.Oracle
{
    /// <inheritdoc />
    public partial class SharedContents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ContentsSplit",
                table: "content_articles",
                type: "BOOLEAN",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "content_contents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    TenantId = table.Column<string>(type: "NVARCHAR2(64)", maxLength: 64, nullable: false),
                    NodeId = table.Column<string>(type: "NVARCHAR2(64)", maxLength: 64, nullable: false),
                    FieldKey = table.Column<string>(type: "NVARCHAR2(128)", maxLength: 128, nullable: false),
                    FieldVersion = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    PostKind = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    ValuesJson = table.Column<string>(type: "NCLOB", nullable: false),
                    Revision = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Version = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    PressId = table.Column<string>(type: "NVARCHAR2(64)", maxLength: 64, nullable: false),
                    CategoryId = table.Column<string>(type: "NVARCHAR2(64)", maxLength: 64, nullable: false),
                    OriginPostId = table.Column<string>(type: "NVARCHAR2(40)", maxLength: 40, nullable: true),
                    SectionId = table.Column<string>(type: "NVARCHAR2(64)", maxLength: 64, nullable: true),
                    AssigneeId = table.Column<string>(type: "NVARCHAR2(128)", maxLength: 128, nullable: true),
                    CreatedBy = table.Column<string>(type: "NVARCHAR2(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: false),
                    Stage = table.Column<string>(type: "NVARCHAR2(40)", maxLength: 40, nullable: false),
                    AcceptedRevision = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    ReviewNote = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_contents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "content_events",
                columns: table => new
                {
                    Id = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    TenantId = table.Column<string>(type: "NVARCHAR2(64)", maxLength: 64, nullable: false),
                    Type = table.Column<string>(type: "NVARCHAR2(40)", maxLength: 40, nullable: false),
                    PostId = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    ContentId = table.Column<Guid>(type: "RAW(16)", nullable: true),
                    Version = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Revision = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    Stage = table.Column<string>(type: "NVARCHAR2(40)", maxLength: 40, nullable: true),
                    PostState = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: true),
                    By = table.Column<string>(type: "NVARCHAR2(128)", maxLength: 128, nullable: false),
                    At = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: false),
                    Detail = table.Column<string>(type: "NVARCHAR2(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "content_pipelines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    TenantId = table.Column<string>(type: "NVARCHAR2(64)", maxLength: 64, nullable: false),
                    Key = table.Column<string>(type: "NVARCHAR2(40)", maxLength: 40, nullable: false),
                    Version = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DefinitionJson = table.Column<string>(type: "NCLOB", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: false),
                    UpdatedBy = table.Column<string>(type: "NVARCHAR2(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_pipelines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "content_post_contents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    PostId = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ContentId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Order = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Required = table.Column<bool>(type: "BOOLEAN", nullable: false),
                    PinnedRevision = table.Column<int>(type: "NUMBER(10)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_post_contents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_content_post_contents_content_articles_PostId",
                        column: x => x.PostId,
                        principalTable: "content_articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "content_post_slugs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    PostId = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Culture = table.Column<string>(type: "NVARCHAR2(35)", maxLength: 35, nullable: false),
                    Slug = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_post_slugs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_content_post_slugs_content_articles_PostId",
                        column: x => x.PostId,
                        principalTable: "content_articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "content_history",
                columns: table => new
                {
                    Id = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    At = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: false),
                    Actor = table.Column<string>(type: "NVARCHAR2(128)", maxLength: 128, nullable: false),
                    Action = table.Column<string>(type: "NVARCHAR2(64)", maxLength: 64, nullable: false),
                    Stage = table.Column<string>(type: "NVARCHAR2(40)", maxLength: 40, nullable: false),
                    Detail = table.Column<string>(type: "NVARCHAR2(1100)", maxLength: 1100, nullable: true),
                    ContentId = table.Column<Guid>(type: "RAW(16)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_content_history_content_contents_ContentId",
                        column: x => x.ContentId,
                        principalTable: "content_contents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "content_revisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    ContentId = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    Revision = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    FieldKey = table.Column<string>(type: "NVARCHAR2(128)", maxLength: 128, nullable: false),
                    FieldVersion = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ValuesJson = table.Column<string>(type: "NCLOB", nullable: false),
                    By = table.Column<string>(type: "NVARCHAR2(128)", maxLength: 128, nullable: false),
                    At = table.Column<DateTimeOffset>(type: "TIMESTAMP(7) WITH TIME ZONE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_revisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_content_revisions_content_contents_ContentId",
                        column: x => x.ContentId,
                        principalTable: "content_contents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_content_contents_AssigneeId",
                table: "content_contents",
                column: "AssigneeId");

            migrationBuilder.CreateIndex(
                name: "IX_content_contents_CategoryId",
                table: "content_contents",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_content_contents_Stage_SectionId",
                table: "content_contents",
                columns: new[] { "Stage", "SectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_content_contents_TenantId",
                table: "content_contents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_content_contents_UpdatedAt",
                table: "content_contents",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_content_events_At",
                table: "content_events",
                column: "At");

            migrationBuilder.CreateIndex(
                name: "IX_content_events_ContentId",
                table: "content_events",
                column: "ContentId");

            migrationBuilder.CreateIndex(
                name: "IX_content_events_PostId",
                table: "content_events",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_content_events_TenantId",
                table: "content_events",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_content_history_ContentId",
                table: "content_history",
                column: "ContentId");

            migrationBuilder.CreateIndex(
                name: "IX_content_pipelines_TenantId",
                table: "content_pipelines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_content_post_contents_ContentId",
                table: "content_post_contents",
                column: "ContentId");

            migrationBuilder.CreateIndex(
                name: "IX_content_post_contents_PostId_ContentId",
                table: "content_post_contents",
                columns: new[] { "PostId", "ContentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_content_post_slugs_Culture_Slug",
                table: "content_post_slugs",
                columns: new[] { "Culture", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_content_post_slugs_PostId_Culture",
                table: "content_post_slugs",
                columns: new[] { "PostId", "Culture" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_content_revisions_ContentId_Revision",
                table: "content_revisions",
                columns: new[] { "ContentId", "Revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "content_events");

            migrationBuilder.DropTable(
                name: "content_history");

            migrationBuilder.DropTable(
                name: "content_pipelines");

            migrationBuilder.DropTable(
                name: "content_post_contents");

            migrationBuilder.DropTable(
                name: "content_post_slugs");

            migrationBuilder.DropTable(
                name: "content_revisions");

            migrationBuilder.DropTable(
                name: "content_contents");

            migrationBuilder.DropColumn(
                name: "ContentsSplit",
                table: "content_articles");
        }
    }
}
