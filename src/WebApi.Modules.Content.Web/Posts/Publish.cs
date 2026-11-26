using soft.square.net.WebApi.Modules.Content.UseCases.Posts;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Publish;

namespace soft.square.net.WebApi.Modules.Content.Web.Posts;

/// <summary>POST /posts/{id}/publish: Desk publishes the current version now.</summary>
public sealed class Publish : IPostEndpoint
{
    public static string Name => "PublishPost";

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder posts, string resourceSegment) =>
        posts.MapPost("/{id:int}/publish", HandleAsync)
            .RequirePermission(ContentPermissions.Publish)
            .ProducesValidationFailure();

    private static async Task<Results<Ok<PostDetail>, ProblemHttpResult>> HandleAsync(int id, VersionRequest r, ISender sender, CancellationToken ct) =>
        (await sender.Send(new PublishPostCommand(id, r.Version), ct)).ToOkResult();
}
