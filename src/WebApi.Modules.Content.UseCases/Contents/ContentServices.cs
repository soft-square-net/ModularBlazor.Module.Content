using System.Text.Json;
using Microsoft.Extensions.Options;
using soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PipelineAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Contents;

/// <summary>The tenant's pipeline: the published one, or the built-in default. Read once per request.</summary>
public sealed class ContentPipelines(IContentStore store)
{
    private PipelineDefinition? _current;

    public async Task<PipelineDefinition> CurrentAsync(CancellationToken ct) =>
        _current ??= (await store.PipelineAsync(ct))?.Definition ?? PipelineDefinition.Default;

    /// <summary>The stage a content is in; a stage a newer definition dropped counts as a work stage nobody may edit in.</summary>
    public static PipelineStage StageOf(PipelineDefinition pipeline, ContentItem content) =>
        pipeline.FindStage(content.Stage) ?? new PipelineStage(content.Stage, StageKind.Work, []);
}

/// <summary>Who may do what to a content: the pipeline's who tokens, and who may see it.</summary>
public sealed class ContentAccess(PostAccess access)
{
    public string Me => access.Me;
    public DateTimeOffset Now => access.Now;
    public PostAccess Posts => access;

    /// <summary>Whether the caller matches one of the tokens: assignee, creator, or a permission held on the content's scopes.</summary>
    public async Task<bool> MatchesAsync(ContentItem content, IReadOnlyList<string> who, CancellationToken ct)
    {
        foreach (var token in who)
        {
            if (content.Matches(token, Me)) return true;
            if (token is PipelineDefinition.Assignee or PipelineDefinition.Creator) continue;
            if (await access.HasAsync(token, content.Scopes, ct)) return true;
        }
        return false;
    }

    public async Task<Result> RequireAsync(ContentItem content, IReadOnlyList<string> who, string what, CancellationToken ct) =>
        await MatchesAsync(content, who, ct) ? Result.Success() : Result.Forbidden($"You may not {what} ({string.Join(" or ", who)}).");

    /// <summary>The creator, the assignee, or anyone holding a content permission on its press or category.</summary>
    public async Task<bool> SeesAsync(ContentItem content, CancellationToken ct) =>
        content.CreatedBy == Me || content.AssigneeId == Me || await access.SeesAsync(content.Scopes, ct)
        || await access.HasAsync(ContentPermissions.Author, content.Scopes, ct) || await access.HasAsync(ContentPermissions.Review, content.Scopes, ct);
}

/// <summary>
/// A post's contents: loading them, the working document, and the publication plan (the pinned revisions and the document
/// assembled from them).
/// </summary>
public sealed class PostContents(IContentStore store)
{
    public async Task<Dictionary<Guid, ContentItem>> LoadAsync(Post post, CancellationToken ct) =>
        post.ContentLinks.Count == 0
            ? []
            : (await store.ContentsAsync(post.ContentLinks.Select(c => c.ContentId).ToList(), ct)).ToDictionary(c => c.Id);

    /// <summary>The working document of the post from its root and these contents, in this order.</summary>
    public static PostDocument Working(Post post, IEnumerable<Guid> order, IReadOnlyDictionary<Guid, ContentItem> contents) =>
        PostComposition.Working(post.Root, order, contents);

    /// <summary>Refreshes the post's working document from its contents' current revisions.</summary>
    public async Task RecomposeAsync(Post post, CancellationToken ct)
    {
        var contents = await LoadAsync(post, ct);
        post.Recompose(Working(post, post.Contents.Select(c => c.ContentId), contents));
    }

    /// <summary>The plan for publishing now: every content with an accepted revision, pinned to it.</summary>
    public async Task<PublishPlan> PlanAsync(Post post, IReadOnlyDictionary<Guid, ContentItem> contents, CancellationToken ct)
    {
        var pins = PostComposition.AcceptedPins(post.Contents, contents);
        return new PublishPlan(pins, await PublishedAsync(post, pins, contents, ct));
    }

    /// <summary>The document of a scheduled post whose time has come: its contents at the revisions pinned when it was scheduled.</summary>
    public async Task<PostDocument> PinnedDocumentAsync(Post post, CancellationToken ct)
    {
        var pins = post.Contents.Where(c => c.PinnedRevision is not null).ToDictionary(c => c.ContentId, c => c.PinnedRevision!.Value);
        return await PublishedAsync(post, pins, await LoadAsync(post, ct), ct);
    }

    private async Task<PostDocument> PublishedAsync(Post post, IReadOnlyDictionary<Guid, int> pins, IReadOnlyDictionary<Guid, ContentItem> contents,
        CancellationToken ct)
    {
        var nodes = new Dictionary<Guid, PostNode>();
        foreach (var (id, revision) in pins)
            nodes[id] = contents.TryGetValue(id, out var content) && content.Revision == revision
                ? content.Node
                : await store.RevisionAsync(id, revision, ct) ?? throw new DomainException($"Revision {revision} of content {id} was not found.");
        return PostComposition.Published(post.Root, post.Contents, pins, (id, _) => nodes[id]);
    }
}

/// <summary>
/// Who edits a post now, with the grants each role needs (the same rules as before contents were shared): the owner
/// (draft, returned, or submitted within the edit window), the assigned reviewer on shift, or Desk in the category.
/// </summary>
public sealed class PostEditors(PostAccess access, IOptions<ContentOptions> options)
{
    public TimeSpan EditWindow => options.Value.OwnerEditWindow;

    public async Task<Result<PostEditor>> RoleAsync(Post post, CancellationToken ct)
    {
        var me = access.Me;
        switch (post.EditorRole(me, access.Now, EditWindow))
        {
            case PostEditor.Owner:
                return PostEditor.Owner;
            case PostEditor.Reviewer:
                var reviewer = await access.RequireAsync(ContentPermissions.Review, post.Scopes, "review in this category now (is your shift over?)", ct);
                return reviewer.IsSuccess ? PostEditor.Reviewer : reviewer.As<PostEditor>();
            case PostEditor.Desk:
                var desk = await access.RequireAsync(ContentPermissions.Publish, post.Scopes, "edit as Desk in this category", ct);
                return desk.IsSuccess ? PostEditor.Desk : desk.As<PostEditor>();
            default:
                // The owner gets the aggregate's explanation (the window closed, or the state); anyone else a refusal.
                if (post.OwnerId == me) return PostEditor.Owner;
                return Result<PostEditor>.Forbidden($"You cannot edit this Post while it is {post.State}.");
        }
    }
}

/// <summary>
/// A whole-document edit of a post (today's editor, <c>PUT /posts/{id}</c>) made into shared contents: the root goes to the
/// post; each child is matched to the post's contents by node id. A changed one gets a new revision (only a content made for
/// this post: a shared one is edited in the library), a new one becomes a content of this post, a missing one is detached
/// (it stays in the library).
/// </summary>
public sealed class PostDocumentSplit(IContentStore store, PostContents postContents, ContentPipelines pipelines)
{
    public async Task<PostChange> EditAsync(Post post, string actor, string title, string body, PostDocument? document, DateTimeOffset now,
        CancellationToken ct)
    {
        if (document is null) return new PostChange(title, body);
        var pipeline = await pipelines.CurrentAsync(ct);
        var existing = await postContents.LoadAsync(post, ct);
        var links = post.Contents.ToDictionary(c => c.ContentId);
        var byNode = existing.Values.GroupBy(c => c.NodeId, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var contents = new Dictionary<Guid, ContentItem>(existing);
        var order = new List<ContentRef>();

        foreach (var child in document.Children ?? [])
        {
            if (child.NodeId is { Length: > 0 } nodeId && byNode.TryGetValue(nodeId, out var content))
            {
                if (Changed(content, child))
                {
                    if (content.OriginPostId != post.StablePublicId)
                        throw new DomainException($"Content '{nodeId}' is shared from another post or the library: edit it in the library.");
                    content.Edit(actor, child, ContentPipelines.StageOf(pipeline, content), now);
                }
                order.Add(new ContentRef(content.Id, links[content.Id].Required));
            }
            else
            {
                var created = ContentItem.Create(actor, post.PressId, post.CategoryId, post.StablePublicId, child, pipeline.StartStage, now);
                store.Add(created);
                contents[created.Id] = created;
                order.Add(new ContentRef(created.Id));
            }
        }
        PostComposition.RequireUniqueNodes(order.Select(o => contents[o.ContentId]));
        var root = document with { Children = [] };
        return new PostChange(title, body, PostComposition.Working(root, order.Select(o => o.ContentId), contents), order);
    }

    /// <summary>For a new post: every child of its document becomes one of its contents.</summary>
    public async Task<(PostDocument? Document, IReadOnlyList<ContentRef>? Contents, IReadOnlyList<ContentItem> Created)> CreateAsync(
        string actor, string pressId, string categoryId, string postPublicId, PostDocument? document, DateTimeOffset now, CancellationToken ct)
    {
        if (document is not { Children.Count: > 0 }) return (document, null, []);
        var pipeline = await pipelines.CurrentAsync(ct);
        var created = document.Children.Select(child => ContentItem.Create(actor, pressId, categoryId, postPublicId, child, pipeline.StartStage, now)).ToList();
        PostComposition.RequireUniqueNodes(created);
        var contents = created.ToDictionary(c => c.Id);
        var root = document with { Children = [] };
        return (PostComposition.Working(root, created.Select(c => c.Id), contents), created.Select(c => new ContentRef(c.Id)).ToList(), created);
    }

    private static bool Changed(ContentItem content, PostNode node) =>
        JsonSerializer.Serialize(node with { NodeId = content.NodeId }, ContentJson.Options) != content.ValuesJson;
}

internal static class ContentJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
