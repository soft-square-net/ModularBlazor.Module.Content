using soft.square.net.WebApi.Modules.Content.UseCases.Posts;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Accept;

namespace soft.square.net.WebApi.Modules.Content.Web.Posts;

/// <summary>POST /posts/{id}/accept: the shift manager accepts the submission.</summary>
public sealed class Accept : IPostEndpoint
{
    public static string Name => "AcceptPost";

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder posts, string resourceSegment) =>
        posts.MapPost("/{id:int}/accept", HandleAsync)
            // Either gets in; the use case asks for the one Content:Newsroom:WorkflowOwnsAssignment calls for.
            .RequirePermission(ContentPermissions.Accept, ContentPermissions.UnitAssign);

    private static async Task<Results<Ok<PostDetail>, ProblemHttpResult>> HandleAsync(int id, AcceptRequest r, ISender sender, CancellationToken ct) =>
        (await sender.Send(new AcceptPostCommand(id, r.Version, r.ShiftId), ct)).ToOkResult();
}
