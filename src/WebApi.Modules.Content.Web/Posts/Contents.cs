using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UseCases.Contents;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts;

namespace soft.square.net.WebApi.Modules.Content.Web.Posts;

/// <summary>The body of <see cref="AttachContent"/>. Its name is the OpenAPI schema name: keep it stable.</summary>
public sealed record PostContentAttachment(int Version, Guid ContentId, int? Index = null, bool Required = true);

/// <summary>The body of <see cref="ReorderContents"/>: every content of the post once, in the new order.</summary>
public sealed record PostContentOrder(int Version, IReadOnlyList<ContentRef> Contents);

/// <summary>The body of <see cref="SetSlugs"/>: culture → slug.</summary>
public sealed record PostSlugsEdit(int Version, IReadOnlyDictionary<string, string> Slugs);

/// <summary>POST /posts/{id}/contents: use a content of the library in the post.</summary>
public sealed class AttachContent : IPostEndpoint
{
    public static string Name => "AttachPostContent";

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder posts, string resourceSegment) =>
        posts.MapPost("/{id:int}/contents", async (int id, PostContentAttachment r, ISender sender, CancellationToken ct) =>
            (await sender.Send(new AttachPostContentCommand(id, r.Version, r.ContentId, r.Index, r.Required), ct)).ToOkResult());
}

/// <summary>DELETE /posts/{id}/contents/{contentId}?version=: stop using a content (it stays in the library).</summary>
public sealed class DetachContent : IPostEndpoint
{
    public static string Name => "DetachPostContent";

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder posts, string resourceSegment) =>
        posts.MapDelete("/{id:int}/contents/{contentId:guid}", async (int id, Guid contentId, int version, ISender sender, CancellationToken ct) =>
            (await sender.Send(new DetachPostContentCommand(id, version, contentId), ct)).ToOkResult());
}

/// <summary>PUT /posts/{id}/contents/order: a new reading order.</summary>
public sealed class ReorderContents : IPostEndpoint
{
    public static string Name => "ReorderPostContents";

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder posts, string resourceSegment) =>
        posts.MapPut("/{id:int}/contents/order", async (int id, PostContentOrder r, ISender sender, CancellationToken ct) =>
            (await sender.Send(new ReorderPostContentsCommand(id, r.Version, r.Contents), ct)).ToOkResult());
}

/// <summary>PUT /posts/{id}/slugs: the post's URL slug per culture.</summary>
public sealed class SetSlugs : IPostEndpoint
{
    public static string Name => "SetPostSlugs";

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder posts, string resourceSegment) =>
        posts.MapPut("/{id:int}/slugs", async (int id, PostSlugsEdit r, ISender sender, CancellationToken ct) =>
            (await sender.Send(new SetPostSlugsCommand(id, r.Version, r.Slugs), ct)).ToOkResult());
}

/// <summary>GET /posts/{id}/events?after=: the post's change feed (its own events and its contents'), for hot reload.</summary>
public sealed class Events : IPostEndpoint
{
    public static string Name => "GetPostEvents";

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder posts, string resourceSegment) =>
        posts.MapGet("/{id:int}/events", async (int id, long? after, int? take, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetPostEventsQuery(id, after ?? 0, take), ct)).ToOkResult());
}
