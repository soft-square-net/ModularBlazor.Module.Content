using soft.square.net.WebApi.Modules.Content.UseCases.Posts;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Approve;

namespace soft.square.net.WebApi.Modules.Content.Web.Posts;

/// <summary>POST /posts/{id}/approve: the assigned reviewer approves.</summary>
public sealed class Approve : IPostEndpoint
{
    public static string Name => "ApprovePost";

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder posts, string resourceSegment) =>
        posts.MapPost("/{id:int}/approve", HandleAsync)
            .RequirePermission(ContentPermissions.Review);

    private static async Task<Results<Ok<PostDetail>, ProblemHttpResult>> HandleAsync(int id, VersionRequest r, ISender sender, CancellationToken ct) =>
        (await sender.Send(new ApprovePostCommand(id, r.Version), ct)).ToOkResult();
}
