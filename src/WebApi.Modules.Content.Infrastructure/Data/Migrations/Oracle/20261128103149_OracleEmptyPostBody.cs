using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace soft.square.net.WebApi.Modules.Content.Infrastructure.Data.Migrations.Oracle
{
    /// <summary>
    /// Oracle stores '' as NULL, so a post with an empty body (a document-only post) could not be saved: the body column
    /// takes NULL (read back as ""). Down puts a single space in the empty bodies, as the old model has no empty value.
    /// </summary>
    public partial class OracleEmptyPostBody : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Body",
                table: "content_articles",
                type: "NVARCHAR2(2000)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(2000)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"content_articles\" SET \"Body\" = N' ' WHERE \"Body\" IS NULL");
            migrationBuilder.AlterColumn<string>(
                name: "Body",
                table: "content_articles",
                type: "NVARCHAR2(2000)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(2000)",
                oldNullable: true);
        }
    }
}
