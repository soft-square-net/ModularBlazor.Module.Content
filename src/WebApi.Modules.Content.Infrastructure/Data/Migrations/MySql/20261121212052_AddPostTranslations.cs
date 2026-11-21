using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace soft.square.net.WebApi.Modules.Content.Infrastructure.Data.Migrations.MySql
{
    /// <inheritdoc />
    public partial class AddPostTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PublishedTranslationsJson",
                table: "content_articles",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TranslationsJson",
                table: "content_articles",
                type: "longtext",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublishedTranslationsJson",
                table: "content_articles");

            migrationBuilder.DropColumn(
                name: "TranslationsJson",
                table: "content_articles");
        }
    }
}
