using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using soft.square.net.WebApi.Modules.Content.Infrastructure;
using soft.square.net.WebApi.Modules.Content.UseCases;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts;

namespace soft.square.net.WebApi.Modules.Content.Web.Configurations;

public static class ServiceConfigs
{
    /// <summary>
    /// Everything the module registers, in the module's own container (never the host's): settings, data access, the
    /// services the use cases share, and the mediator with every handler.
    /// </summary>
    public static IServiceCollection AddServiceConfigs(this IServiceCollection services, IConfiguration settings)
    {
        services.Configure<ContentOptions>(settings.GetSection("Content"));
        services.AddInfrastructureServices();

        // Shared by the Post use cases.
        services.AddScoped<PostAccess>();
        services.AddScoped<PostDetails>();
        services.AddScoped<PostChanges>();

        services.AddMediatorSourceGen();
        return services;
    }
}
