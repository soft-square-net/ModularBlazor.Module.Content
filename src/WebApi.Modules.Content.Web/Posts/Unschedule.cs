using soft.square.net.WebApi.Modules.Content.UseCases.Posts;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Unschedule;

namespace soft.square.net.WebApi.Modules.Content.Web.Posts;

/// <summary>POST /posts/{id}/unschedule: take a scheduled Post off the schedule; it is Approved again.</summary>
public sealed class Unschedule : IPostEndpoint
{
    public static string Name => "UnschedulePost";

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder posts, string resourceSegment) =>
        posts.MapPost("/{id:int}/unschedule", HandleAsync)
            .RequirePermission(ContentPermissions.Publish);

    private static async Task<Results<Ok<PostDetail>, ProblemHttpResult>> HandleAsync(int id, VersionRequest r, ISender sender, CancellationToken ct) =>
        (await sender.Send(new UnschedulePostCommand(id, r.Version), ct)).ToOkResult();
}
