using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace soft.square.net.WebApi.Modules.Content.Infrastructure.Data.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class NewsroomCutover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignedShiftId",
                schema: "content",
                table: "articles");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewerAwaySince",
                schema: "content",
                table: "articles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WorkflowProjectEnded",
                schema: "content",
                table: "articles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowProjectId",
                schema: "content",
                table: "articles",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "post_create_keys",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PostId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_create_keys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_post_create_keys_articles_PostId",
                        column: x => x.PostId,
                        principalSchema: "content",
                        principalTable: "articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_post_create_keys_KeyHash",
                schema: "content",
                table: "post_create_keys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_post_create_keys_PostId",
                schema: "content",
                table: "post_create_keys",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_post_create_keys_TenantId",
                schema: "content",
                table: "post_create_keys",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "post_create_keys",
                schema: "content");

            migrationBuilder.DropColumn(
                name: "ReviewerAwaySince",
                schema: "content",
                table: "articles");

            migrationBuilder.DropColumn(
                name: "WorkflowProjectEnded",
                schema: "content",
                table: "articles");

            migrationBuilder.DropColumn(
                name: "WorkflowProjectId",
                schema: "content",
                table: "articles");

            migrationBuilder.AddColumn<string>(
                name: "AssignedShiftId",
                schema: "content",
                table: "articles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }
    }
}
