using System.Reflection;
using Microsoft.EntityFrameworkCore;
using soft.square.net.WebApi.Contracts.Tenancy;
using soft.square.net.WebApi.Data;
using soft.square.net.WebApi.Modules.Content.Core.CategoryAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PressAggregate;

namespace soft.square.net.WebApi.Modules.Content.Infrastructure.Data;

/// <summary>
/// The module's tenant data: schema "content" in each tenant's database. The entity mappings are the
/// <c>IEntityTypeConfiguration</c> classes in <c>Data/Config</c>; the migrations, one set per engine, are in
/// <c>Data/Migrations/&lt;Provider&gt;</c>.
/// </summary>
public sealed class ContentDbContext(DbContextOptions<ContentDbContext> options, ITenantContext tenant) : TenantDbContext(options, tenant)
{
    protected override string Schema => "content";

    public DbSet<Press> Presses => Set<Press>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Post> Posts => Set<Post>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        if (Database.ProviderName == "Oracle.EntityFrameworkCore")
        {
            // Oracle maps an unbounded string to NVARCHAR2(2000); Post documents may be up to 2 MB.
            modelBuilder.Entity<Post>().Property(a => a.DocumentJson).HasColumnType("NCLOB");
            modelBuilder.Entity<Post>().Property(a => a.PublishedDocumentJson).HasColumnType("NCLOB");
        }
    }
}
