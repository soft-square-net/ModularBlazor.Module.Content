using soft.square.net.WebApi.Modules.Content.UseCases.Posts;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Create;

namespace soft.square.net.WebApi.Modules.Content.Web.Posts;

/// <summary>POST /posts: write a new Post (a draft); 201 with the Post.</summary>
public sealed class Create : IPostEndpoint
{
    public static string Name => "CreatePost";

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder posts, string resourceSegment) =>
        posts.MapPost("/", (NewPost request, ISender sender, CancellationToken ct) => HandleAsync(request, resourceSegment, sender, ct))
            .RequirePermission(ContentPermissions.Author);

    private static async Task<Results<Created<PostDetail>, ProblemHttpResult>> HandleAsync(NewPost request, string resourceSegment, ISender sender, CancellationToken ct) =>
        (await sender.Send(new CreatePostCommand(request.CategoryId, request.Title, request.Body ?? "", request.PostKind, request.Document), ct))
            .ToCreatedResult(post => $"{resourceSegment}/{post.Id}");
}
