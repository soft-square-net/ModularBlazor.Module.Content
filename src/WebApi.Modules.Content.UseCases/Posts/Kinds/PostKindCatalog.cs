using Microsoft.Extensions.Logging;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Kinds;

/// <summary>A post kind as DynamicFields publishes it: a root post field with its static URL prefix.</summary>
/// <param name="Version">The field's newest published version: the one a new post's root is made against.</param>
public sealed record PostKindInfo(string Key, string Title, string UrlPrefix, string? SchemaType, int Version = 1);

/// <summary>
/// The current tenant's post kinds: DynamicFields' published root post fields
/// (<c>GET /api/dynamicfields/catalog/root-post-fields</c>). A key missing from the list is unknown, unpublished or no
/// longer a root. Implementations cache the list per tenant for a short time.
/// </summary>
public interface IPostKindCatalog
{
    /// <summary>The kinds; throws <see cref="PostKindCatalogUnavailableException"/> when DynamicFields cannot answer.</summary>
    Task<IReadOnlyList<PostKindInfo>> ListAsync(CancellationToken ct);

    /// <summary>
    /// The keys of the values a published field version defines (<c>GET /api/dynamicfields/fields/{key}/versions/{version}</c>);
    /// throws <see cref="PostKindCatalogUnavailableException"/> when DynamicFields cannot answer.
    /// </summary>
    Task<IReadOnlyList<string>> ValueKeysAsync(string key, int version, CancellationToken ct);
}

/// <summary>DynamicFields could not be reached, or refused the call: the kinds are unknown for now.</summary>
public sealed class PostKindCatalogUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// The rules on a post's kind (wave 4, decision D3):
/// <list type="bullet">
/// <item>a new post's kind must be a published DynamicFields root post field;</item>
/// <item>a post whose kind has no such field yet (a legacy kind such as <c>post.video</c> before its field exists) can
/// still be edited, but it cannot be published or scheduled until the field exists;</item>
/// <item>when DynamicFields cannot answer, creating and publishing are refused with 503, and reads go without a prefix.</item>
/// </list>
/// </summary>
public sealed class PostKindRules(IPostKindCatalog catalog, ILoggerFactory loggers)
{
    private readonly ILogger _log = loggers.CreateLogger("soft.square.net.WebApi.Modules.Content.UseCases.PostKinds");

    /// <summary>The kind for a new post: its key, when DynamicFields publishes it as a root post field.</summary>
    public async Task<Result<PostKindInfo>> ForNewPostAsync(string? kind, CancellationToken ct)
    {
        var key = PostKinds.Normalize(kind);
        if (!PostKinds.IsKey(key))
            return Result<PostKindInfo>.Invalid(new ValidationError(nameof(Post.PostKind),
                $"'{kind}' is not a post kind: a DynamicFields root post field key such as {PostKinds.Article}."));
        var found = await FindAsync(key, ct);
        if (!found.IsSuccess) return found.As<PostKindInfo>();
        return found.Value is { } info
            ? info
            : Result<PostKindInfo>.Invalid(new ValidationError(nameof(Post.PostKind),
                $"'{key}' is not a published DynamicFields root post field. Publish that field (with its URL prefix) first, or choose another kind."));
    }

    /// <summary>
    /// The root of a new post of this kind: made against the field's published version, with the title as its headline when
    /// the field has one. Unavailable when DynamicFields cannot answer.
    /// </summary>
    public async Task<Result<PostDocument>> NewRootAsync(PostKindInfo kind, string title, CancellationToken ct)
    {
        try
        {
            var keys = await catalog.ValueKeysAsync(kind.Key, kind.Version, ct);
            return PostDocument.NewRoot(kind.Key, kind.Version, keys.Contains(PostDocument.HeadlineKey, StringComparer.Ordinal), title);
        }
        catch (PostKindCatalogUnavailableException e)
        {
            _log.LogWarning(e, "The post kind {Kind} could not be read from DynamicFields: {Reason}", kind.Key, e.Message);
            return Result<PostDocument>.Unavailable($"The post kinds could not be read from DynamicFields ({e.Message}); try again later.");
        }
    }

    /// <summary>Every published kind, by title; Unavailable when DynamicFields cannot answer.</summary>
    public async Task<Result<IReadOnlyList<PostKindInfo>>> ListAsync(CancellationToken ct)
    {
        try
        {
            return Result<IReadOnlyList<PostKindInfo>>.Success(
                [.. (await catalog.ListAsync(ct)).OrderBy(k => k.Title, StringComparer.OrdinalIgnoreCase)]);
        }
        catch (PostKindCatalogUnavailableException e)
        {
            _log.LogWarning(e, "The post kinds could not be read from DynamicFields: {Reason}", e.Message);
            return Result<IReadOnlyList<PostKindInfo>>.Unavailable($"The post kinds could not be read from DynamicFields ({e.Message}); try again later.");
        }
    }

    /// <summary>The kind a post is published (or scheduled) under: refused while DynamicFields has no published root field for it.</summary>
    public async Task<Result<PostKindInfo>> ForPublishingAsync(Post post, CancellationToken ct)
    {
        var found = await FindAsync(post.PostKind, ct);
        if (!found.IsSuccess) return found.As<PostKindInfo>();
        return found.Value is { } info
            ? info
            : Result<PostKindInfo>.Conflict(
                $"Post {post.Id} is of kind '{post.PostKind}', which has no published DynamicFields root post field, so it has no URL and " +
                $"cannot be published yet. Publish the root post field '{post.PostKind}' (with its URL prefix) in DynamicFields first; the post can still be edited.");
    }

    /// <summary>The kind's current URL prefix for display; null when it has none or DynamicFields cannot answer (logged).</summary>
    public async Task<string?> UrlPrefixOrNullAsync(string kind, CancellationToken ct)
    {
        var found = await FindAsync(kind, ct);
        return found.IsSuccess ? found.Value?.UrlPrefix : null;
    }

    /// <summary>The kind (null: not published), or Unavailable when DynamicFields cannot answer.</summary>
    private async Task<Result<PostKindInfo?>> FindAsync(string key, CancellationToken ct)
    {
        try
        {
            var kinds = await catalog.ListAsync(ct);
            return Result<PostKindInfo?>.Success(kinds.FirstOrDefault(k => string.Equals(k.Key, key, StringComparison.Ordinal)));
        }
        catch (PostKindCatalogUnavailableException e)
        {
            _log.LogWarning(e, "The post kinds could not be read from DynamicFields: {Reason}", e.Message);
            return Result<PostKindInfo?>.Unavailable($"The post kinds could not be read from DynamicFields ({e.Message}); try again later.");
        }
    }
}
