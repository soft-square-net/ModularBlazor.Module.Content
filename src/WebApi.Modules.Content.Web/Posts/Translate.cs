using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Translate;

namespace soft.square.net.WebApi.Modules.Content.Web.Posts;

/// <summary>The body of <see cref="Translate"/>. Its name is the OpenAPI schema name: keep it stable.</summary>
public sealed record PostTranslationEdit(int Version, IReadOnlyDictionary<string, IReadOnlyDictionary<string, TranslatedText>> Nodes);

/// <summary>
/// PUT /posts/{id}/translations/{language}: add or replace the document's translation into one language (the
/// DynamicFields translate dialog's result). The workflow state does not change.
/// </summary>
public sealed class Translate : IPostEndpoint
{
    public static string Name => "TranslatePost";

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder posts, string resourceSegment) =>
        posts.MapPut("/{id:int}/translations/{language}", HandleAsync);

    private static async Task<Results<Ok<PostDetail>, ProblemHttpResult>> HandleAsync(int id, string language, PostTranslationEdit edit,
        ISender sender, CancellationToken ct) =>
        (await sender.Send(new TranslatePostCommand(id, edit.Version, language, edit.Nodes), ct)).ToOkResult();
}

/// <summary>DELETE /posts/{id}/translations/{language}?version=: remove the document's translation into one language.</summary>
public sealed class RemoveTranslation : IPostEndpoint
{
    public static string Name => "RemovePostTranslation";

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder posts, string resourceSegment) =>
        posts.MapDelete("/{id:int}/translations/{language}", HandleAsync);

    private static async Task<Results<Ok<PostDetail>, ProblemHttpResult>> HandleAsync(int id, string language, int version,
        ISender sender, CancellationToken ct) =>
        (await sender.Send(new RemovePostTranslationCommand(id, version, language), ct)).ToOkResult();
}
