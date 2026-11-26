using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using soft.square.net.WebApi.Modules.Content.Infrastructure;
using soft.square.net.WebApi.Modules.Content.UseCases;
using soft.square.net.WebApi.Modules.Content.Core.FeedAggregate;
using soft.square.net.WebApi.Modules.Content.UseCases.Contents;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts;
using soft.square.net.WebApi.Contracts.Live;
using soft.square.net.WebApi.Modules.Content.UseCases.Live;
using soft.square.net.WebApi.SharedKernel;

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
        services.AddScoped<soft.square.net.WebApi.Modules.Content.UseCases.Posts.Translate.PostTranslators>();
        services.AddScoped<PostEditors>();
        services.AddScoped<DeskPublishing>();
        services.AddScoped<PostContentEditing>();
        services.AddScoped<soft.square.net.WebApi.Modules.Content.UseCases.Posts.Kinds.PostKindRules>();

        // Shared contents, the pipeline and the change feed.
        services.AddScoped<ContentPipelines>();
        services.AddScoped<ContentAccess>();
        services.AddScoped<ContentDetails>();
        services.AddScoped<ContentChanges>();
        services.AddScoped<PostContents>();
        services.AddScoped<PostDocumentSplit>();
        services.AddScoped<FeedReader>();
        services.AddScoped<IDomainEventHandler<FeedEvent>, FeedWriter>(); // first: the outbox row, then the effects
        services.AddScoped<IDomainEventHandler<FeedEvent>, RecomposePostsOnContentChange>();

        // Live: a nudge on content:post:{id} once feed rows are committed, and the post's scopes for the hub's check.
        services.AddScoped<IFeedCommitObserver, LivePostPublisher>();
        services.AddScoped<ILiveScopeResolver, PostLiveScopeResolver>();

        services.AddMediatorSourceGen();
        return services;
    }
}
