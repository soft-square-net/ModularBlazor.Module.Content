using soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;
using soft.square.net.WebApi.Modules.Content.Core.FeedAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PipelineAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Contents;

/// <summary>
/// The module's unit of work for changes that span posts and contents (one save, all or nothing), and the reads the
/// repositories' specifications cannot express. Implemented over the module's DbContext; everything tracked is written by
/// <see cref="SaveChangesAsync"/> (or by a repository's Update, which saves the same context).
/// </summary>
public interface IContentStore
{
    /// <summary>Adds a new content to the save (no write yet).</summary>
    void Add(ContentItem content);

    /// <summary>Removes a content in the save (the caller has checked that no post uses it).</summary>
    void Remove(ContentItem content);

    /// <summary>Adds an outbox row to the save (the feed writer; never called by use cases).</summary>
    void Add(FeedEntry entry);

    void Add(Pipeline pipeline);

    /// <summary>The contents with these ids, tracked (including ones added to this save but not written yet).</summary>
    Task<IReadOnlyList<ContentItem>> ContentsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    /// <summary>The values of one revision of a content.</summary>
    Task<PostNode?> RevisionAsync(Guid contentId, int revision, CancellationToken ct);

    /// <summary>The posts that use this content, tracked with their contents.</summary>
    Task<IReadOnlyList<Post>> PostsUsingAsync(Guid contentId, CancellationToken ct);

    /// <summary>The ids of the posts that use these contents, untracked (the live fan-out of a content's feed rows).</summary>
    Task<IReadOnlyList<(Guid ContentId, int PostId)>> PostIdsUsingAsync(IReadOnlyCollection<Guid> contentIds, CancellationToken ct);

    /// <summary>A post's press and category, untracked; null when there is no such post in the tenant.</summary>
    Task<(string PressId, string CategoryId)?> PostScopesAsync(int postId, CancellationToken ct);

    /// <summary>Whether any post uses this content.</summary>
    Task<bool> IsUsedAsync(Guid contentId, CancellationToken ct);

    /// <summary>The tenant's published pipeline, tracked; null when it uses the default.</summary>
    Task<Pipeline?> PipelineAsync(CancellationToken ct);

    /// <summary>The id of another post that has this slug in this culture, if any.</summary>
    Task<int?> SlugOwnerAsync(string culture, string slug, int exceptPostId, CancellationToken ct);

    /// <summary>
    /// Feed rows after <paramref name="after"/> and no newer than <paramref name="notAfter"/>, oldest first: all of the tenant's,
    /// or (with <paramref name="postId"/>) the post's own and those of the contents it uses.
    /// </summary>
    Task<IReadOnlyList<FeedEntry>> FeedAsync(long after, DateTimeOffset notAfter, int take, int? postId, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
