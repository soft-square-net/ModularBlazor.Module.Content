using soft.square.net.WebApi.Modules.Content.UseCases.Posts;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Assign;

namespace soft.square.net.WebApi.Modules.Content.Web.Posts;

/// <summary>POST /posts/{id}/assign: the shift manager assigns an editor on shift.</summary>
public sealed class Assign : IPostEndpoint
{
    public static string Name => "AssignPost";

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder posts, string resourceSegment) =>
        posts.MapPost("/{id:int}/assign", HandleAsync)
            .RequirePermission(ContentPermissions.Assign);

    private static async Task<Results<Ok<PostDetail>, ProblemHttpResult>> HandleAsync(int id, Assignment r, ISender sender, CancellationToken ct) =>
        (await sender.Send(new AssignPostCommand(id, r.Version, r.AssigneeId, r.ShiftId), ct)).ToOkResult();
}
