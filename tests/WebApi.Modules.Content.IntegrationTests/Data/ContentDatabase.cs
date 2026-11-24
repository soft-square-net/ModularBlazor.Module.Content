using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using soft.square.net.WebApi.Contracts.Tenancy;
using soft.square.net.WebApi.Data;
using soft.square.net.WebApi.Modules.Content.Core.FeedAggregate;
using soft.square.net.WebApi.Modules.Content.Infrastructure.Data;
using soft.square.net.WebApi.Modules.Content.UseCases.Contents;

namespace soft.square.net.WebApi.Modules.Content.IntegrationTests.Data;

/// <summary>
/// One in-memory SQLite database with the module's model (EnsureCreated, not the per-provider migrations: those are
/// checked with <c>dotnet ef migrations has-pending-model-changes</c>). Each <see cref="For"/> call is a new context,
/// as a new request would get, for the given tenant, with the module's domain event handlers (the feed's outbox, the
/// recomposition of posts) running in its saves as in the application.
/// </summary>
public sealed class ContentDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly DbContextOptions<ContentDbContext> _options;

    public ContentDatabase()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<ContentDbContext>().UseSqlite(_connection).Options;
        using var context = For("acme");
        context.Database.EnsureCreated();
    }

    public ContentDbContext For(string? tenantId)
    {
        ContentDbContext? context = null;
        var services = new ServiceCollection();
        services.AddScoped<DomainEventDispatcher>();
        services.AddScoped<IContentStore>(_ => new ContentStore(context!));
        services.AddScoped<PostContents>();
        services.AddScoped<IDomainEventHandler<FeedEvent>, FeedWriter>();
        services.AddScoped<IDomainEventHandler<FeedEvent>, RecomposePostsOnContentChange>();
        var provider = services.BuildServiceProvider().CreateScope().ServiceProvider;
        var options = new DbContextOptionsBuilder<ContentDbContext>(_options).UseApplicationServiceProvider(provider).Options;
        return context = new ContentDbContext(options, new TestTenant(tenantId));
    }

    public void Dispose() => _connection.Dispose();

    private sealed class TestTenant(string? id) : ITenantContext
    {
        public TenantInfo? Tenant { get; } = id is null ? null : new TenantInfo(id, id, TenantStatus.Active, [], "", "");
        public string? ResolvedBy => "test";
    }
}
