using soft.square.net.WebApi.Modules.Content.UseCases.Posts.PublishDue;

namespace soft.square.net.WebApi.Modules.Content.Web.Posts;

/// <summary>POST /posts/publish-due: publish every scheduled Post that is due.</summary>
public sealed class PublishDue : IPostEndpoint
{
    public static string Name => "PublishDuePosts";

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder posts, string resourceSegment) =>
        posts.MapPost("/publish-due", HandleAsync)
            .RequirePermission(ContentPermissions.Publish);

    private static async Task<Results<Ok<IReadOnlyList<int>>, ProblemHttpResult>> HandleAsync(ISender sender, CancellationToken ct) =>
        (await sender.Send(new PublishDuePostsCommand(), ct)).ToOkResult();
}
