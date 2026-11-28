using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.Web.Posts;

/// <summary>
/// The body of <see cref="Create"/>. Its name is the OpenAPI schema name: keep it stable. <see cref="PostKind"/> is a
/// published DynamicFields root post field key (default <c>post.article</c>); the old enum names (<c>Article</c>,
/// <c>News</c>, <c>Video</c>, <c>Reportage</c>, <c>PhotoStory</c>, <c>Other</c>) are still accepted for one release.
/// <see cref="IdempotencyKey"/> (optional, at most 200 characters) makes the request safe to repeat: unique in the tenant,
/// a repeat by the same user answers 201 with the post made first, and another user's use of it is a 409.
/// </summary>
public sealed record NewPost(string CategoryId, string Title, string? Body, string PostKind = PostKinds.Article, PostDocument? Document = null,
    IReadOnlyList<string>? CategoryIds = null, string? IdempotencyKey = null);
