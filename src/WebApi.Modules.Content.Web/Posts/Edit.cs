using soft.square.net.WebApi.Modules.Content.UseCases.Posts;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Edit;

namespace soft.square.net.WebApi.Modules.Content.Web.Posts;

/// <summary>PUT /posts/{id}: edit as the owner, the assigned reviewer or Desk (the use case decides which).</summary>
public sealed class Edit : IPostEndpoint
{
    public static string Name => "EditPost";

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder posts, string resourceSegment) =>
        posts.MapPut("/{id:int}", HandleAsync);

    private static async Task<Results<Ok<PostDetail>, ProblemHttpResult>> HandleAsync(int id, PostEdit edit, ISender sender, CancellationToken ct) =>
        (await sender.Send(new EditPostCommand(id, edit.Version, edit.Title, edit.Body ?? "", edit.Document), ct)).ToOkResult();
}
