using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace soft.square.net.WebApi.Modules.Content.Infrastructure.Data.Migrations.Oracle
{
    /// <summary>
    /// A post's body and translations (and their published copies) become NCLOB: Oracle maps an unbounded string to
    /// NVARCHAR2(2000), so longer ones could not be saved. Oracle cannot alter NVARCHAR2 to NCLOB or back (ORA-22858), so
    /// each column is rebuilt: a new column, the values copied, the old one dropped, the new one renamed. Down cuts values
    /// to 2000 characters.
    /// </summary>
    public partial class OracleLongPostText : Migration
    {
        private const string Table = "\"content_articles\"";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            Rebuild(migrationBuilder, "Body", "NCLOB", "\"Body\"");
            Rebuild(migrationBuilder, "PublishedBody", "NCLOB", "\"PublishedBody\"");
            Rebuild(migrationBuilder, "PublishedTranslationsJson", "NCLOB", "\"PublishedTranslationsJson\"");
            Rebuild(migrationBuilder, "TranslationsJson", "NCLOB DEFAULT N'[]'", "\"TranslationsJson\"", required: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            static string Cut(string column) => $"DBMS_LOB.SUBSTR(\"{column}\", 2000, 1)";
            Rebuild(migrationBuilder, "Body", "NVARCHAR2(2000)", Cut("Body"));
            Rebuild(migrationBuilder, "PublishedBody", "NVARCHAR2(2000)", Cut("PublishedBody"));
            Rebuild(migrationBuilder, "PublishedTranslationsJson", "NVARCHAR2(2000)", Cut("PublishedTranslationsJson"));
            Rebuild(migrationBuilder, "TranslationsJson", "NVARCHAR2(2000) DEFAULT N'[]'", Cut("TranslationsJson"), required: true);
        }

        /// <summary>Replaces <paramref name="column"/> with a column of <paramref name="type"/> holding <paramref name="value"/>.</summary>
        private static void Rebuild(MigrationBuilder migrationBuilder, string column, string type, string value, bool required = false)
        {
            var next = $"\"{column}_next\"";
            migrationBuilder.Sql($"ALTER TABLE {Table} ADD ({next} {type})");
            migrationBuilder.Sql($"UPDATE {Table} SET {next} = {value}");
            migrationBuilder.Sql($"ALTER TABLE {Table} DROP COLUMN \"{column}\"");
            migrationBuilder.Sql($"ALTER TABLE {Table} RENAME COLUMN {next} TO \"{column}\"");
            if (required) migrationBuilder.Sql($"ALTER TABLE {Table} MODIFY (\"{column}\" NOT NULL)");
        }
    }
}
