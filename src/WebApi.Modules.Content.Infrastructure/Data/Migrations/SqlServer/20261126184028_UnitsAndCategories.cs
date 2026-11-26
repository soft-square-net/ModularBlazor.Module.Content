using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace soft.square.net.WebApi.Modules.Content.Infrastructure.Data.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class UnitsAndCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Name",
                schema: "content",
                table: "categories",
                newName: "NameEn");

            migrationBuilder.AddColumn<Guid>(
                name: "UnitId",
                schema: "content",
                table: "contents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitPath",
                schema: "content",
                table: "contents",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameAr",
                schema: "content",
                table: "categories",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UnitId",
                schema: "content",
                table: "articles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitPath",
                schema: "content",
                table: "articles",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "integration_inbox",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_inbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "integration_outbox",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Envelope = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_outbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "post_categories",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PostId = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_post_categories_articles_PostId",
                        column: x => x.PostId,
                        principalSchema: "content",
                        principalTable: "articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_contents_UnitId",
                schema: "content",
                table: "contents",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_articles_UnitId",
                schema: "content",
                table: "articles",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_integration_inbox_ProcessedAt",
                schema: "content",
                table: "integration_inbox",
                column: "ProcessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_integration_outbox_SentAt_OccurredAt",
                schema: "content",
                table: "integration_outbox",
                columns: new[] { "SentAt", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_post_categories_CategoryId",
                schema: "content",
                table: "post_categories",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_post_categories_PostId_CategoryId",
                schema: "content",
                table: "post_categories",
                columns: new[] { "PostId", "CategoryId" },
                unique: true);
            // Data: the Arabic names start as the English ones, and each post's category becomes its first post_categories row.
            foreach (var sql in UnitsAndCategoriesSql.Up(t => $"[content].[{t}]", c => $"[{c}]"))
                migrationBuilder.Sql(sql);

            migrationBuilder.AlterColumn<string>(
                name: "NameAr",
                schema: "content",
                table: "categories",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "integration_inbox",
                schema: "content");

            migrationBuilder.DropTable(
                name: "integration_outbox",
                schema: "content");

            migrationBuilder.DropTable(
                name: "post_categories",
                schema: "content");

            migrationBuilder.DropIndex(
                name: "IX_contents_UnitId",
                schema: "content",
                table: "contents");

            migrationBuilder.DropIndex(
                name: "IX_articles_UnitId",
                schema: "content",
                table: "articles");

            migrationBuilder.DropColumn(
                name: "UnitId",
                schema: "content",
                table: "contents");

            migrationBuilder.DropColumn(
                name: "UnitPath",
                schema: "content",
                table: "contents");

            migrationBuilder.DropColumn(
                name: "NameAr",
                schema: "content",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "UnitId",
                schema: "content",
                table: "articles");

            migrationBuilder.DropColumn(
                name: "UnitPath",
                schema: "content",
                table: "articles");

            migrationBuilder.RenameColumn(
                name: "NameEn",
                schema: "content",
                table: "categories",
                newName: "Name");
        }
    }
}
