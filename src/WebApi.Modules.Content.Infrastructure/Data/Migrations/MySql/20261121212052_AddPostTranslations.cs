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

            // MySQL rejects a literal DEFAULT on a TEXT column: add it nullable, fill it, then require it. The application
            // always writes TranslationsJson, so this engine needs no column default (found by the migration harness).
            migrationBuilder.AddColumn<string>(
                name: "TranslationsJson",
                table: "content_articles",
                type: "longtext",
                nullable: true);

            migrationBuilder.Sql("UPDATE content_articles SET TranslationsJson = '[]' WHERE TranslationsJson IS NULL");

            migrationBuilder.AlterColumn<string>(
                name: "TranslationsJson",
                table: "content_articles",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true);
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
