using soft.square.net.WebApi.Modules.Content.UseCases.Posts;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Transfer;

namespace soft.square.net.WebApi.Modules.Content.Web.Posts;

/// <summary>POST /posts/{id}/transfer: hand unfinished review work to the next shift.</summary>
public sealed class Transfer : IPostEndpoint
{
    public static string Name => "TransferPost";

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder posts, string resourceSegment) =>
        posts.MapPost("/{id:int}/transfer", HandleAsync)
            .RequirePermission(ContentPermissions.Transfer);

    private static async Task<Results<Ok<PostDetail>, ProblemHttpResult>> HandleAsync(int id, TransferRequest r, ISender sender, CancellationToken ct) =>
        (await sender.Send(new TransferPostCommand(id, r.Version, r.FromShift), ct)).ToOkResult();
}
