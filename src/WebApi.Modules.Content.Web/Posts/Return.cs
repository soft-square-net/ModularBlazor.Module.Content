using soft.square.net.WebApi.Modules.Content.UseCases.Posts;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Return;

namespace soft.square.net.WebApi.Modules.Content.Web.Posts;

/// <summary>POST /posts/{id}/return: back to the author (shift Manager or Desk).</summary>
public sealed class Return : IPostEndpoint
{
    public static string Name => "ReturnPost";

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder posts, string resourceSegment) =>
        posts.MapPost("/{id:int}/return", HandleAsync)
            .RequirePermission(ContentPermissions.Return);

    private static async Task<Results<Ok<PostDetail>, ProblemHttpResult>> HandleAsync(int id, ReturnRequest r, ISender sender, CancellationToken ct) =>
        (await sender.Send(new ReturnPostCommand(id, r.Version, r.Reason), ct)).ToOkResult();
}
