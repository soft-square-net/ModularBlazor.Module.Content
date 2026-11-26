using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using soft.square.net.WebApi.Contracts.Messaging;
using soft.square.net.WebApi.Contracts.Tenancy;
using soft.square.net.WebApi.Data;
using soft.square.net.WebApi.Modules.Content.UseCases.Contents;
using soft.square.net.WebApi.Modules.Content.Infrastructure.Data;
using soft.square.net.WebApi.Modules.Content.Infrastructure.Http;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Kinds;
using soft.square.net.WebApi.Modules.Content.UseCases.Units;

namespace soft.square.net.WebApi.Modules.Content.Infrastructure;

public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// The module's data access, in the module's own container: the tenant DbContext, and through it
    /// <c>IRepository&lt;T&gt;</c> / <c>IReadRepository&lt;T&gt;</c> for every aggregate root (Post, Press, Category) and the
    /// domain event dispatcher. The connection comes from the tenant, so there is no connection string here. Also the
    /// post kinds (<see cref="IPostKindCatalog"/>), read from DynamicFields through the gateway, and the Organization units
    /// (<see cref="IUnitLookup"/>), read from Organization's local directory through the gateway. Integration events: the
    /// outbox (<c>content.*</c>) and the inbox with the handlers of <c>shifts.section-moved.v1</c> and <c>org.unit-*.v1</c>
    /// (the same patterns are the manifest's Subscriptions).
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddTenantDbContext<ContentDbContext>();
        // After AddTenantDbContext: tenant initializers run in registration order, so the schema is migrated first.
        services.AddSingleton<ITenantInitializer, ContentBackfill>();
        services.AddScoped<IContentStore, ContentStore>();
        services.AddIntegrationEvents<ContentDbContext>(o => o.Source = "content");
        services.AddIntegrationHandler<SectionMovedHandler>(SectionMovedHandler.Type);
        services.AddIntegrationHandler<OrgUnitEventsHandler>(OrgUnitEventsHandler.Pattern);

        // The post kinds, from DynamicFields over HTTP (cached per tenant in the module's own cache).
        services.AddHttpContextAccessor();
        services.AddSingleton<IMemoryCache>(_ => new MemoryCache(new MemoryCacheOptions { SizeLimit = 1024 }));
        services.AddHttpClient<IPostKindCatalog, DynamicFieldsPostKinds>(client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddHttpClient(OrganizationUnitLookup.ClientName, client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddScoped<IUnitLookup, OrganizationUnitLookup>();
        return services;
    }
}
