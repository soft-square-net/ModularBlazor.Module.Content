using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using soft.square.net.WebApi.Contracts;
using soft.square.net.WebApi.Modules.Content.UseCases;
using soft.square.net.WebApi.Modules.Content.UseCases.Live;
using soft.square.net.WebApi.Modules.Content.Web.Configurations;

namespace soft.square.net.WebApi.Modules.Content.Web;

/// <summary>
/// Contents module: presses, categories and Posts with the newsroom workflow of the auth design. Tenant data (schema
/// "content" in each tenant's database). Rights come from grants: standing ones (author, editor) from admins, shift
/// ones (accept, assign, transfer, return, review, publish) from the shifts module's rota, so they hold only while the
/// shift runs.
/// This is the composition root (the template's Program.cs): the runtime creates it, reads the manifest and calls
/// <see cref="ConfigureServices"/> on the module's own container; the endpoints are in <see cref="ContentRoutes"/>.
/// </summary>
public sealed class ContentModule : IRuntimeModule
{
    public ModuleManifest Manifest { get; } = new("content", "Content", "0.1.0", "content")
    {
        Description = "Newsroom posts (article, news, video, reportage, and other post kinds), taxonomy, review and publishing workflow.",
        DefaultSettings = new Dictionary<string, string?> { ["Content:OwnerEditWindow"] = "1.00:00:00", ["Content:EventDelay"] = "00:00:02" },
        Permissions = ContentPermissions.All,
        // content:post:{id}: a nudge for every new row of the post's feed (see PostLive).
        LiveTopics = [PostLive.Topic]
    };

    public void ConfigureServices(IServiceCollection services, IConfiguration settings) => services.AddServiceConfigs(settings);
}
