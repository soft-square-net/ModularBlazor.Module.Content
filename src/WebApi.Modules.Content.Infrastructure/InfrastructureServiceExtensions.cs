using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using soft.square.net.WebApi.Contracts.Tenancy;
using soft.square.net.WebApi.Data;
using soft.square.net.WebApi.Modules.Content.UseCases.Contents;
using soft.square.net.WebApi.Modules.Content.Infrastructure.Data;
using soft.square.net.WebApi.Modules.Content.Infrastructure.Http;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Kinds;

namespace soft.square.net.WebApi.Modules.Content.Infrastructure;

public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// The module's data access, in the module's own container: the tenant DbContext, and through it
    /// <c>IRepository&lt;T&gt;</c> / <c>IReadRepository&lt;T&gt;</c> for every aggregate root (Post, Press, Category) and the
    /// domain event dispatcher. The connection comes from the tenant, so there is no connection string here. Also the
    /// post kinds (<see cref="IPostKindCatalog"/>), read from DynamicFields through the gateway.
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddTenantDbContext<ContentDbContext>();
        // After AddTenantDbContext: tenant initializers run in registration order, so the schema is migrated first.
        services.AddSingleton<ITenantInitializer, ContentBackfill>();
        services.AddScoped<IContentStore, ContentStore>();

        // The post kinds, from DynamicFields over HTTP (cached per tenant in the module's own cache).
        services.AddHttpContextAccessor();
        services.AddSingleton<IMemoryCache>(_ => new MemoryCache(new MemoryCacheOptions { SizeLimit = 1024 }));
        services.AddHttpClient<IPostKindCatalog, DynamicFieldsPostKinds>(client => client.Timeout = TimeSpan.FromSeconds(10));
        return services;
    }
}
