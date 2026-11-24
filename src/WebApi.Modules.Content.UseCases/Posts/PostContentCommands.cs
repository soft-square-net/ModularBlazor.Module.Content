using soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UseCases.Contents;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts;

/// <summary>
/// Changes which contents a post uses, and their order, as an edit of the post: by whoever may edit it now (owner, assigned
/// reviewer, Desk: <see cref="PostEditors"/>), with the same effects (Desk's edit cancels a schedule and needs re-approval).
/// A newly used content must be one the caller sees, not archived, and its node id must be new to the post.
/// </summary>
public sealed class PostContentEditing(IContentStore store, PostContents postContents, PostEditors editors, ContentAccess access)
{
    public async Task<Result> EditAsync(Post post, IReadOnlyList<ContentRef> next, IReadOnlyDictionary<string, string>? slugs,
        IReadOnlyDictionary<Guid, ContentItem>? added, CancellationToken ct)
    {
        var role = await editors.RoleAsync(post, ct);
        if (!role.IsSuccess) return ToResult(role);

        var contents = await postContents.LoadAsync(post, ct);
        foreach (var (id, content) in added ?? new Dictionary<Guid, ContentItem>()) contents[id] = content;
        var current = post.ContentLinks.Select(c => c.ContentId).ToHashSet();
        var missing = next.Select(n => n.ContentId).Where(id => !contents.ContainsKey(id)).ToList();
        if (missing.Count > 0)
            foreach (var content in await store.ContentsAsync(missing, ct)) contents[content.Id] = content;
        foreach (var wanted in next.Where(n => !current.Contains(n.ContentId)))
        {
            if (!contents.TryGetValue(wanted.ContentId, out var content) || !await access.SeesAsync(content, ct))
                return Result.NotFound($"Content {wanted.ContentId} was not found.");
            if (content.IsArchived) return Result.Conflict($"Content {wanted.ContentId} is archived: it cannot be used by another post.");
        }
        PostComposition.RequireUniqueNodes(next.Select(n => contents[n.ContentId]));
        var working = PostContents.Working(post, next.Select(n => n.ContentId), contents);
        post.Edit(role.Value, access.Me, access.Now, editors.EditWindow, new PostChange(Document: working, Contents: next, Slugs: slugs));
        return Result.Success();
    }

    private static Result ToResult(Result<PostEditor> failure) => failure.Status switch
    {
        ResultStatus.Forbidden => Result.Forbidden([.. failure.Errors]),
        ResultStatus.NotFound => Result.NotFound([.. failure.Errors]),
        _ => Result.Conflict([.. failure.Errors])
    };
}

/// <summary>Use a content of the library in a post, at <see cref="Index"/> (the end by default).</summary>
public sealed record AttachPostContentCommand(int Id, int Version, Guid ContentId, int? Index, bool Required = true) : ICommand<Result<PostDetail>>;

public sealed class AttachPostContentHandler(PostChanges changes, PostContentEditing editing) : ICommandHandler<AttachPostContentCommand, Result<PostDetail>>
{
    public async ValueTask<Result<PostDetail>> Handle(AttachPostContentCommand command, CancellationToken ct) =>
        await changes.ChangeAsync(command.Id, command.Version, post =>
        {
            var next = post.Contents.Select(c => new ContentRef(c.ContentId, c.Required)).ToList();
            if (next.Any(c => c.ContentId == command.ContentId)) return Task.FromResult(Result.Conflict("The post already uses this content."));
            next.Insert(Math.Clamp(command.Index ?? next.Count, 0, next.Count), new ContentRef(command.ContentId, command.Required));
            return editing.EditAsync(post, next, null, null, ct);
        }, ct);
}

/// <summary>Stop using a content in a post (it stays in the library).</summary>
public sealed record DetachPostContentCommand(int Id, int Version, Guid ContentId) : ICommand<Result<PostDetail>>;

public sealed class DetachPostContentHandler(PostChanges changes, PostContentEditing editing) : ICommandHandler<DetachPostContentCommand, Result<PostDetail>>
{
    public async ValueTask<Result<PostDetail>> Handle(DetachPostContentCommand command, CancellationToken ct) =>
        await changes.ChangeAsync(command.Id, command.Version, post =>
        {
            var next = post.Contents.Where(c => c.ContentId != command.ContentId).Select(c => new ContentRef(c.ContentId, c.Required)).ToList();
            if (next.Count == post.ContentLinks.Count) return Task.FromResult(Result.NotFound("The post does not use this content."));
            return editing.EditAsync(post, next, null, null, ct);
        }, ct);
}

/// <summary>
/// Put a post's contents in a new reading order: <see cref="Contents"/> lists every one of them once (a content's
/// <c>Required</c> flag may change with it).
/// </summary>
public sealed record ReorderPostContentsCommand(int Id, int Version, IReadOnlyList<ContentRef> Contents) : ICommand<Result<PostDetail>>;

public sealed class ReorderPostContentsHandler(PostChanges changes, PostContentEditing editing)
    : ICommandHandler<ReorderPostContentsCommand, Result<PostDetail>>
{
    public async ValueTask<Result<PostDetail>> Handle(ReorderPostContentsCommand command, CancellationToken ct) =>
        await changes.ChangeAsync(command.Id, command.Version, post =>
        {
            var current = post.ContentLinks.Select(c => c.ContentId).OrderBy(id => id).ToList();
            var given = (command.Contents ?? []).Select(c => c.ContentId).OrderBy(id => id).ToList();
            if (!current.SequenceEqual(given))
                return Task.FromResult(Result.Invalid(new ValidationError("List every content of the post once: use attach and detach to change which.")));
            return editing.EditAsync(post, command.Contents!, null, null, ct);
        }, ct);
}

/// <summary>Set a post's URL slugs, one per culture (the ones left out are removed). A slug is unique per culture in the tenant.</summary>
public sealed record SetPostSlugsCommand(int Id, int Version, IReadOnlyDictionary<string, string> Slugs) : ICommand<Result<PostDetail>>;

public sealed class SetPostSlugsHandler(PostChanges changes, PostContentEditing editing, IContentStore store)
    : ICommandHandler<SetPostSlugsCommand, Result<PostDetail>>
{
    public async ValueTask<Result<PostDetail>> Handle(SetPostSlugsCommand command, CancellationToken ct) =>
        await changes.ChangeAsync(command.Id, command.Version, async post =>
        {
            var slugs = (command.Slugs ?? new Dictionary<string, string>())
                .ToDictionary(s => s.Key.Trim(), s => (s.Value ?? "").Trim(), StringComparer.OrdinalIgnoreCase);
            foreach (var (culture, slug) in slugs)
                if (await store.SlugOwnerAsync(culture, slug, post.Id, ct) is { } other)
                    return Result.Conflict($"Post {other} already has the slug '{slug}' in '{culture}'.");
            var current = post.Contents.Select(c => new ContentRef(c.ContentId, c.Required)).ToList();
            return await editing.EditAsync(post, current, slugs, null, ct);
        }, ct);
}
