using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace soft.square.net.WebApi.Modules.Content.Infrastructure.Data.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class WorkItemStages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The first built-in pipeline's stage keys become the work-item states (contents and a published pipeline).
            foreach (var sql in WorkItemStagesSql.Up(t => $"content.{t}", c => $"\"{c}\""))
                migrationBuilder.Sql(sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var sql in WorkItemStagesSql.Down(t => $"content.{t}", c => $"\"{c}\""))
                migrationBuilder.Sql(sql);
        }
    }
}
