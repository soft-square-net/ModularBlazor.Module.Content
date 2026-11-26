using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace soft.square.net.WebApi.Modules.Content.Infrastructure.Data.Migrations.Oracle
{
    /// <inheritdoc />
    public partial class PostKindsAsFieldKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PostKind",
                table: "content_contents",
                type: "NVARCHAR2(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "content_articles",
                type: "NVARCHAR2(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "post.article",
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(32)",
                oldMaxLength: 32,
                oldDefaultValue: "Article");

            migrationBuilder.AddColumn<string>(
                name: "PublishedUrlPrefix",
                table: "content_articles",
                type: "NVARCHAR2(200)",
                maxLength: 200,
                nullable: true);

            // The old enum names become DynamicFields root field keys (columns and stored JSON), after the columns widened.
            foreach (var sql in PostKindKeysSql.Up(t => t.StartsWith("content_") ? $"\"{t}\"" : $"\"content_{t}\"", c => $"\"{c}\"", national: true))
                migrationBuilder.Sql(sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The keys back to the enum names, before the columns narrow again.
            foreach (var sql in PostKindKeysSql.Down(t => t.StartsWith("content_") ? $"\"{t}\"" : $"\"content_{t}\"", c => $"\"{c}\"", national: true))
                migrationBuilder.Sql(sql);

            migrationBuilder.DropColumn(
                name: "PublishedUrlPrefix",
                table: "content_articles");

            migrationBuilder.AlterColumn<string>(
                name: "PostKind",
                table: "content_contents",
                type: "NVARCHAR2(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "content_articles",
                type: "NVARCHAR2(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Article",
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(64)",
                oldMaxLength: 64,
                oldDefaultValue: "post.article");
        }
    }
}
