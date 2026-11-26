using DotNet.Testcontainers.Containers;
using soft.square.net.WebApi.Data;
using Testcontainers.MsSql;
using Testcontainers.MySql;
using Testcontainers.Oracle;
using Testcontainers.PostgreSql;

namespace soft.square.net.WebApi.Modules.Content.MigrationTests;

/// <summary>PostgreSQL: the module's tables in the schema <c>content</c>.</summary>
public sealed class PostgreSqlMigrations : MigrationHarness
{
    protected override DatabaseProvider Provider => DatabaseProvider.PostgreSql;
    protected override string OfflineConnection => "Host=localhost;Database=content_tests";
    protected override IDatabaseContainer CreateContainer() => new PostgreSqlBuilder("postgres:17").Build();
    protected override string Table(string name) => $"content.\"{name}\"";
    protected override string Column(string name) => $"\"{name}\"";
}

/// <summary>SQL Server: the module's tables in the schema <c>content</c>.</summary>
public sealed class SqlServerMigrations : MigrationHarness
{
    protected override DatabaseProvider Provider => DatabaseProvider.SqlServer;
    protected override string OfflineConnection => "Server=localhost;Database=content_tests;TrustServerCertificate=true";
    protected override IDatabaseContainer CreateContainer() => new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    protected override string Table(string name) => $"[content].[{name}]";
    protected override string Column(string name) => $"[{name}]";
}

/// <summary>MySQL: no schemas, so the module's tables are prefixed <c>content_</c> (unless their name already starts with it).</summary>
public sealed class MySqlMigrations : MigrationHarness
{
    protected override DatabaseProvider Provider => DatabaseProvider.MySql;
    protected override string OfflineConnection => "Server=localhost;Database=content_tests;User ID=test;Password=test";
    protected override IDatabaseContainer CreateContainer() => new MySqlBuilder("mysql:8.4").Build();
    protected override string Table(string name) => name.StartsWith("content_", StringComparison.Ordinal) ? $"`{name}`" : $"`content_{name}`";
    protected override string Column(string name) => $"`{name}`";
}

/// <summary>Oracle (Free 23ai): the module's tables in the user's schema, prefixed <c>content_</c> (unless their name already starts with it).</summary>
public sealed class OracleMigrations : MigrationHarness
{
    protected override DatabaseProvider Provider => DatabaseProvider.Oracle;
    protected override string OfflineConnection => "User Id=test;Password=test;Data Source=localhost:1521/FREEPDB1";
    protected override IDatabaseContainer CreateContainer() => new OracleBuilder("gvenzl/oracle-free:23-slim").Build();
    protected override string Table(string name) => name.StartsWith("content_", StringComparison.Ordinal) ? $"\"{name}\"" : $"\"content_{name}\"";
    protected override string Column(string name) => $"\"{name}\"";
}
