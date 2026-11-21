using soft.square.net.WebApi.Modules.Content.UseCases.Posts;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.GetById;

namespace soft.square.net.WebApi.Modules.Content.Web.Posts;

/// <summary>GET /posts/{id}: one Post with its history.</summary>
public sealed class GetById : IPostEndpoint
{
    public static string Name => "GetPost";

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder posts, string resourceSegment) =>
        posts.MapGet("/{id:int}", HandleAsync);

    private static async Task<Results<Ok<PostDetail>, ProblemHttpResult>> HandleAsync(int id, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetPostByIdQuery(id), ct)).ToOkResult();
}
