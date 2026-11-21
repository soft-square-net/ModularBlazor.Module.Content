using soft.square.net.WebApi.Modules.Content.UseCases.Posts;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.List;

namespace soft.square.net.WebApi.Modules.Content.Web.Posts;

/// <summary>GET /posts?view=: one of the caller's lists (Mine by default).</summary>
public sealed class List : IPostEndpoint
{
    public static string Name => "ListPosts";

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder posts, string resourceSegment) =>
        posts.MapGet("/", HandleAsync)
            .WithSummary("Posts by post kind: article, news, video, reportage, and more. ?view=Mine | Queue | Review | Desk.");

    private static async Task<Results<Ok<IReadOnlyList<PostSummary>>, ProblemHttpResult>> HandleAsync(PostView? view, ISender sender, CancellationToken ct) =>
        (await sender.Send(new ListPostsQuery(view ?? PostView.Mine), ct)).ToOkResult();
}
