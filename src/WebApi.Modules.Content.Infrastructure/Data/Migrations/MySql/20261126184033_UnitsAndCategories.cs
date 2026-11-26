using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace soft.square.net.WebApi.Modules.Content.Infrastructure.Data.Migrations.MySql
{
    /// <inheritdoc />
    public partial class UnitsAndCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Name",
                table: "content_categories",
                newName: "NameEn");

            migrationBuilder.AddColumn<Guid>(
                name: "UnitId",
                table: "content_contents",
                type: "char(36)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitPath",
                table: "content_contents",
                type: "varchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameAr",
                table: "content_categories",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UnitId",
                table: "content_articles",
                type: "char(36)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitPath",
                table: "content_articles",
                type: "varchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "content_integration_inbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Type = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Source = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_integration_inbox", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "content_integration_outbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Type = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    TenantId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Envelope = table.Column<string>(type: "longtext", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_integration_outbox", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "content_post_categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    PostId = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_post_categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_content_post_categories_content_articles_PostId",
                        column: x => x.PostId,
                        principalTable: "content_articles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_content_contents_UnitId",
                table: "content_contents",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_content_articles_UnitId",
                table: "content_articles",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_content_integration_inbox_ProcessedAt",
                table: "content_integration_inbox",
                column: "ProcessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_content_integration_outbox_SentAt_OccurredAt",
                table: "content_integration_outbox",
                columns: new[] { "SentAt", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_content_post_categories_CategoryId",
                table: "content_post_categories",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_content_post_categories_PostId_CategoryId",
                table: "content_post_categories",
                columns: new[] { "PostId", "CategoryId" },
                unique: true);
            // Data: the Arabic names start as the English ones, and each post's category becomes its first post_categories row.
            foreach (var sql in UnitsAndCategoriesSql.Up(t => t.StartsWith("content_") ? $"`{t}`" : $"`content_{t}`", c => $"`{c}`"))
                migrationBuilder.Sql(sql);

            migrationBuilder.AlterColumn<string>(
                name: "NameAr",
                table: "content_categories",
                type: "varchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(200)",
                oldMaxLength: 200,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "content_integration_inbox");

            migrationBuilder.DropTable(
                name: "content_integration_outbox");

            migrationBuilder.DropTable(
                name: "content_post_categories");

            migrationBuilder.DropIndex(
                name: "IX_content_contents_UnitId",
                table: "content_contents");

            migrationBuilder.DropIndex(
                name: "IX_content_articles_UnitId",
                table: "content_articles");

            migrationBuilder.DropColumn(
                name: "UnitId",
                table: "content_contents");

            migrationBuilder.DropColumn(
                name: "UnitPath",
                table: "content_contents");

            migrationBuilder.DropColumn(
                name: "NameAr",
                table: "content_categories");

            migrationBuilder.DropColumn(
                name: "UnitId",
                table: "content_articles");

            migrationBuilder.DropColumn(
                name: "UnitPath",
                table: "content_articles");

            migrationBuilder.RenameColumn(
                name: "NameEn",
                table: "content_categories",
                newName: "Name");
        }
    }
}
