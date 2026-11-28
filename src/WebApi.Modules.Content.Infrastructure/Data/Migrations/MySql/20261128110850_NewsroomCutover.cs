using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace soft.square.net.WebApi.Modules.Content.Infrastructure.Data.Migrations.MySql
{
    /// <inheritdoc />
    public partial class NewsroomCutover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignedShiftId",
                table: "content_articles");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewerAwaySince",
                table: "content_articles",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WorkflowProjectEnded",
                table: "content_articles",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowProjectId",
                table: "content_articles",
                type: "char(36)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "content_post_create_keys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    TenantId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    KeyHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    OwnerId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                    PostId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_post_create_keys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_content_post_create_keys_content_articles_PostId",
                        column: x => x.PostId,
                        principalTable: "content_articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_content_post_create_keys_KeyHash",
                table: "content_post_create_keys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_content_post_create_keys_PostId",
                table: "content_post_create_keys",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_content_post_create_keys_TenantId",
                table: "content_post_create_keys",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "content_post_create_keys");

            migrationBuilder.DropColumn(
                name: "ReviewerAwaySince",
                table: "content_articles");

            migrationBuilder.DropColumn(
                name: "WorkflowProjectEnded",
                table: "content_articles");

            migrationBuilder.DropColumn(
                name: "WorkflowProjectId",
                table: "content_articles");

            migrationBuilder.AddColumn<string>(
                name: "AssignedShiftId",
                table: "content_articles",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true);
        }
    }
}
