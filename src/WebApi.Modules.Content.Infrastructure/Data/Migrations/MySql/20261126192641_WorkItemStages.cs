using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace soft.square.net.WebApi.Modules.Content.Infrastructure.Data.Migrations.MySql
{
    /// <inheritdoc />
    public partial class WorkItemStages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The first built-in pipeline's stage keys become the work-item states (contents and a published pipeline).
            foreach (var sql in WorkItemStagesSql.Up(t => t.StartsWith("content_") ? t : $"content_{t}", c => $"`{c}`"))
                migrationBuilder.Sql(sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in WorkItemStagesSql.Down(t => t.StartsWith("content_") ? t : $"content_{t}", c => $"`{c}`"))
                migrationBuilder.Sql(sql);
        }
    }
}
