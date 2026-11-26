using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace soft.square.net.WebApi.Modules.Content.Infrastructure.Data.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class PostKindsAsFieldKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PostKind",
                schema: "content",
                table: "contents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                schema: "content",
                table: "articles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "post.article",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldDefaultValue: "Article");

            migrationBuilder.AddColumn<string>(
                name: "PublishedUrlPrefix",
                schema: "content",
                table: "articles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            // The old enum names become DynamicFields root field keys (columns and stored JSON), after the columns widened.
            foreach (var sql in PostKindKeysSql.Up(t => $"content.{t}", c => $"\"{c}\""))
                migrationBuilder.Sql(sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The keys back to the enum names, before the columns narrow again.
            foreach (var sql in PostKindKeysSql.Down(t => $"content.{t}", c => $"\"{c}\""))
                migrationBuilder.Sql(sql);

            migrationBuilder.DropColumn(
                name: "PublishedUrlPrefix",
                schema: "content",
                table: "articles");

            migrationBuilder.AlterColumn<string>(
                name: "PostKind",
                schema: "content",
                table: "contents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                schema: "content",
                table: "articles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Article",
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldDefaultValue: "post.article");
        }
    }
}
