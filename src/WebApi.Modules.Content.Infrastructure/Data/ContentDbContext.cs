using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using soft.square.net.WebApi.Contracts.Tenancy;
using soft.square.net.WebApi.Data;
using soft.square.net.WebApi.Modules.Content.Core.CategoryAggregate;
using soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;
using soft.square.net.WebApi.Modules.Content.Core.FeedAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PipelineAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PressAggregate;

namespace soft.square.net.WebApi.Modules.Content.Infrastructure.Data;

/// <summary>
/// The module's tenant data: schema "content" in each tenant's database. The entity mappings are the
/// <c>IEntityTypeConfiguration</c> classes in <c>Data/Config</c>; the migrations, one set per engine, are in
/// <c>Data/Migrations/&lt;Provider&gt;</c>.
/// </summary>
public sealed class ContentDbContext : TenantDbContext
{
    private readonly FeedCommits _feed;

    public ContentDbContext(DbContextOptions<ContentDbContext> options, ITenantContext tenant) : base(options, tenant)
    {
        _feed = new FeedCommits(this);
        _feed.Follow();
    }

    protected override string Schema => "content";

    /// <summary>
    /// The platform's integration outbox and inbox: the module publishes <c>content.*</c> events and follows
    /// <c>shifts.section-moved.v1</c> and <c>org.unit-moved.v1</c>.
    /// </summary>
    protected override bool UsesIntegrationEvents => true;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.AddInterceptors(_feed); // commits and rollbacks of explicit transactions
    }

    /// <summary>
    /// The platform's save (domain event handlers, then the write), then the live nudges for the feed rows it wrote, once
    /// committed (<see cref="FeedCommits"/>). The nudges never fail the save.
    /// </summary>
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        await _feed.SavedAsync(acceptAllChangesOnSuccess, cancellationToken);
        return result;
    }

    public DbSet<Press> Presses => Set<Press>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<ContentItem> Contents => Set<ContentItem>();
    public DbSet<ContentRevision> ContentRevisions => Set<ContentRevision>();
    public DbSet<PostContent> PostContents => Set<PostContent>();
    public DbSet<PostSlug> PostSlugs => Set<PostSlug>();
    public DbSet<PostCategory> PostCategories => Set<PostCategory>();
    public DbSet<Pipeline> Pipelines => Set<Pipeline>();
    public DbSet<FeedEntry> Events => Set<FeedEntry>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        if (Database.ProviderName == "Oracle.EntityFrameworkCore")
        {
            // Oracle maps an unbounded string to NVARCHAR2(2000); Post documents may be up to 2 MB, and bodies and
            // translations have no limit either. None of these is compared in a query (Oracle cannot compare an NCLOB).
            modelBuilder.Entity<Post>().Property(a => a.DocumentJson).HasColumnType("NCLOB");
            modelBuilder.Entity<Post>().Property(a => a.PublishedDocumentJson).HasColumnType("NCLOB");
            modelBuilder.Entity<Post>().Property(a => a.Body).HasColumnType("NCLOB");
            modelBuilder.Entity<Post>().Property(a => a.PublishedBody).HasColumnType("NCLOB");
            modelBuilder.Entity<Post>().Property(a => a.TranslationsJson).HasColumnType("NCLOB");
            modelBuilder.Entity<Post>().Property(a => a.PublishedTranslationsJson).HasColumnType("NCLOB");
            modelBuilder.Entity<ContentItem>().Property(c => c.ValuesJson).HasColumnType("NCLOB");
            modelBuilder.Entity<ContentRevision>().Property(r => r.ValuesJson).HasColumnType("NCLOB");
            modelBuilder.Entity<Pipeline>().Property(p => p.DefinitionJson).HasColumnType("NCLOB");

            // Oracle stores '' as NULL, and a post's body may be empty (a document-only post): the column takes NULL, and EF
            // reads it into the post's nullable backing field, so Body reads back as "". The model's other required strings
            // are never empty (the domain refuses blank values).
            modelBuilder.Entity<Post>().Property(a => a.Body).IsRequired(false);
        }

        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            // The integration tests' engine cannot compare DateTimeOffset values (the feed's hold-back): store them as ticks.
            foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetProperties())
                         .Where(p => p.ClrType == typeof(DateTimeOffset) || p.ClrType == typeof(DateTimeOffset?)))
                property.SetValueConverter(new DateTimeOffsetToBinaryConverter());
        }
    }
}
