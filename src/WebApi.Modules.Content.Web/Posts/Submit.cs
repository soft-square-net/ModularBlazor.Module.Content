using soft.square.net.WebApi.Modules.Content.UseCases.Posts;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Submit;

namespace soft.square.net.WebApi.Modules.Content.Web.Posts;

/// <summary>POST /posts/{id}/submit: the owner submits the Post for review.</summary>
public sealed class Submit : IPostEndpoint
{
    public static string Name => "SubmitPost";

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder posts, string resourceSegment) =>
        posts.MapPost("/{id:int}/submit", HandleAsync);

    private static async Task<Results<Ok<PostDetail>, ProblemHttpResult>> HandleAsync(int id, VersionRequest r, ISender sender, CancellationToken ct) =>
        (await sender.Send(new SubmitPostCommand(id, r.Version), ct)).ToOkResult();
}
