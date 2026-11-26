using soft.square.net.WebApi.Modules.Content.UseCases.Posts;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Schedule;

namespace soft.square.net.WebApi.Modules.Content.Web.Posts;

/// <summary>POST /posts/{id}/schedule: Desk schedules the current version.</summary>
public sealed class Schedule : IPostEndpoint
{
    public static string Name => "SchedulePost";

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder posts, string resourceSegment) =>
        posts.MapPost("/{id:int}/schedule", HandleAsync)
            .RequirePermission(ContentPermissions.Publish)
            .ProducesValidationFailure();

    private static async Task<Results<Ok<PostDetail>, ProblemHttpResult>> HandleAsync(int id, ScheduleRequest r, ISender sender, CancellationToken ct) =>
        (await sender.Send(new SchedulePostCommand(id, r.Version, r.PublishAt), ct)).ToOkResult();
}
