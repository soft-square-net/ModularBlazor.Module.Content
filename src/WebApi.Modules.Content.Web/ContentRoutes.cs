using Carter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using soft.square.net.WebApi.Modules.Content.Web.Extensions;
using Automation = soft.square.net.WebApi.Modules.Content.Web.Automation;
using Categories = soft.square.net.WebApi.Modules.Content.Web.Categories;
using Posts = soft.square.net.WebApi.Modules.Content.Web.Posts;
using Presses = soft.square.net.WebApi.Modules.Content.Web.Presses;

namespace soft.square.net.WebApi.Modules.Content.Web;

/// <summary>
/// The module's route table, and its only Carter module (the runtime maps the Carter modules of the entry assembly,
/// under <c>/api/content</c>). Every endpoint is listed here once, in the order of the OpenAPI document. A new
/// endpoint: add its file under the resource folder, then one line here.
/// </summary>
public sealed class ContentRoutes : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var content = app.MapGroup("").AddEndpointFilter<ContentProblemsFilter>();

        Presses.List.Map(content);
        Presses.Add.Map(content);
        Categories.List.Map(content);
        Categories.Add.Map(content);

        MapPosts(content.MapGroup("/posts"), "posts", documented: true);
        // Compatibility routes: the same handlers, left out of the OpenAPI document so generated clients only use /posts.
        MapPosts(content.MapGroup("/articles").ExcludeFromDescription(), "articles", documented: false);
        MapPosts(content.MapGroup("/works").ExcludeFromDescription(), "works", documented: false);
        Posts.GetPublished.Map(content);

        // Its own group, mapped after the others, so the OpenAPI document lists these last.
        var automation = app.MapGroup("").AddEndpointFilter<ContentProblemsFilter>();
        Automation.ListAccepted.Map(automation);
        Automation.AutoAssign.Map(automation);
        Automation.HandOver.Map(automation);
    }

    private static void MapPosts(RouteGroupBuilder posts, string segment, bool documented)
    {
        Post<Posts.List>(posts, segment, documented);
        Post<Posts.GetById>(posts, segment, documented);
        Post<Posts.Create>(posts, segment, documented);
        Post<Posts.Edit>(posts, segment, documented);
        Post<Posts.Submit>(posts, segment, documented);
        Post<Posts.Accept>(posts, segment, documented);
        Post<Posts.Assign>(posts, segment, documented);
        Post<Posts.Approve>(posts, segment, documented);
        Post<Posts.Return>(posts, segment, documented);
        Post<Posts.Transfer>(posts, segment, documented);
        Post<Posts.Schedule>(posts, segment, documented);
        Post<Posts.Publish>(posts, segment, documented);
        Post<Posts.PublishDue>(posts, segment, documented);
        Post<Posts.Translate>(posts, segment, documented);
        Post<Posts.RemoveTranslation>(posts, segment, documented);
    }

    private static void Post<TEndpoint>(RouteGroupBuilder posts, string segment, bool documented) where TEndpoint : IPostEndpoint
    {
        var endpoint = TEndpoint.Map(posts, segment);
        if (documented) endpoint.WithName("Content_" + TEndpoint.Name);
    }
}
