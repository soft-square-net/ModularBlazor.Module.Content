using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace soft.square.net.WebApi.Modules.Content.Infrastructure.Data.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddPostTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PublishedTranslationsJson",
                schema: "content",
                table: "articles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TranslationsJson",
                schema: "content",
                table: "articles",
                type: "text",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublishedTranslationsJson",
                schema: "content",
                table: "articles");

            migrationBuilder.DropColumn(
                name: "TranslationsJson",
                schema: "content",
                table: "articles");
        }
    }
}
