using System.Text.Json;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Root;

namespace soft.square.net.WebApi.Modules.Content.Web.Posts;

/// <summary>The body of <see cref="SetRoot"/>. Its name is the OpenAPI schema name: keep it stable.</summary>
public sealed record PostRootEdit(int Version, int FieldVersion, JsonElement Content, JsonElement? Style = null);

/// <summary>PUT /posts/{id}/root: the values of the post's kind (the document's root); its contents, title and body stay.</summary>
public sealed class SetRoot : IPostEndpoint
{
    public static string Name => "SetPostRoot";

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder posts, string resourceSegment) =>
        posts.MapPut("/{id:int}/root", HandleAsync)
            .WithSummary("Set the values of the post's kind (the root of its document); also gives a legacy post its kind's root.");

    private static async Task<Results<Ok<PostDetail>, ProblemHttpResult>> HandleAsync(int id, PostRootEdit r, ISender sender, CancellationToken ct) =>
        (await sender.Send(new ChangePostRootCommand(id, r.Version, r.FieldVersion, r.Content, r.Style), ct)).ToOkResult();
}
