using soft.square.net.WebApi.Modules.Content.Core.CategoryAggregate;
using soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate.Specifications;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts;
using soft.square.net.WebApi.Modules.Content.UseCases.Validation;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Contents;

/// <summary>
/// The shape of every content change: load it (tracked), check that the caller sees it and sent its current version, apply
/// the change (rights check, then the aggregate's method), save, and return it as the caller now sees it. Posts that use
/// it get their working document refreshed in the same save (<see cref="RecomposePostsOnContentChange"/>).
/// </summary>
public sealed class ContentChanges(IContentStore store, ContentAccess access, ContentDetails details)
{
    public async Task<Result<ContentDetail>> ChangeAsync(Guid id, int version, Func<ContentItem, Task<Result>> change, CancellationToken ct)
    {
        var content = (await store.ContentsAsync([id], ct)).FirstOrDefault();
        if (content is null || !await access.SeesAsync(content, ct)) return ContentDetails.NotFound(id);
        if (content.Version != version)
            return Result<ContentDetail>.Conflict($"Content {id} is at version {content.Version}, not {version}; reload it and try again.");
        var changed = await change(content);
        if (!changed.IsSuccess) return changed.As<ContentDetail>();
        await store.SaveChangesAsync(ct); // the version is a concurrency token: a racing change loses with 409
        return await details.GetAsync(id, ct);
    }
}

/// <summary>
/// Make a content in the library (the caller needs the author right in its category), in the pipeline's start stage.
/// With <see cref="PostId"/> (and its <see cref="PostVersion"/>) it is attached to that post at once, at <see cref="Index"/>
/// (the end by default), by someone who may edit the post now; its category is then the post's unless given.
/// While the workflow owns assignment, naming a first <see cref="AssigneeId"/> needs <c>content.unit.assign</c>.
/// </summary>
public sealed record CreateContentCommand(string? CategoryId, PostNode Values, string? SectionId = null, string? AssigneeId = null,
    int? PostId = null, int? PostVersion = null, int? Index = null, bool Required = true) : ICommand<Result<ContentDetail>>;

public sealed class CreateContentHandler(IReadRepository<Category> categories, IRepository<Post> posts, IContentStore store,
    ContentAccess access, ContentPipelines pipelines, PostContentEditing editing, ContentDetails details, AssignmentRights rights)
    : ICommandHandler<CreateContentCommand, Result<ContentDetail>>
{
    public async ValueTask<Result<ContentDetail>> Handle(CreateContentCommand command, CancellationToken ct)
    {
        Post? post = null;
        if (command.PostId is { } postId)
        {
            post = await posts.FirstOrDefaultAsync(new PostByIdSpec(postId), ct);
            if (post is null) return Result<ContentDetail>.NotFound($"Post {postId} was not found.");
            if (post.Version != command.PostVersion)
                return Result<ContentDetail>.Conflict($"Post {postId} is at version {post.Version}, not {command.PostVersion}; reload it and try again.");
        }
        var categoryId = command.CategoryId ?? post?.PrimaryCategoryId;
        var category = categoryId is null ? null : await categories.GetByIdAsync(categoryId, ct);
        if (category is null) return Result<ContentDetail>.Conflict($"Unknown category '{categoryId}'.");
        var author = await access.Posts.RequireAsync(ContentPermissions.Author, [AccessScope.Press(category.PressId), AccessScope.Category(category.Id)],
            "write in this category", ct);
        if (!author.IsSuccess) return author.As<ContentDetail>();
        if (rights.WorkflowOwns && !string.IsNullOrWhiteSpace(command.AssigneeId))
        {
            var assigner = await access.Posts.RequireAsync(ContentPermissions.UnitAssign,
                post?.Scopes ?? [AccessScope.Press(category.PressId), AccessScope.Category(category.Id)],
                "name an assignee for a new content while the newsroom workflow owns assignment (create it, then assign it)", ct);
            if (!assigner.IsSuccess) return assigner.As<ContentDetail>();
        }

        var pipeline = await pipelines.CurrentAsync(ct);
        var content = ContentItem.Create(access.Me, category.PressId, category.Id, post?.StablePublicId, command.Values with { NodeId = "" },
            pipeline.StartStage, access.Now, command.SectionId, command.AssigneeId);
        store.Add(content);
        if (post is not null)
        {
            var next = post.Contents.Select(c => new ContentRef(c.ContentId, c.Required)).ToList();
            next.Insert(Math.Clamp(command.Index ?? next.Count, 0, next.Count), new ContentRef(content.Id, command.Required));
            var attached = await editing.EditAsync(post, next, null, new Dictionary<Guid, ContentItem> { [content.Id] = content }, ct);
            if (!attached.IsSuccess) return attached.As<ContentDetail>();
        }
        await store.SaveChangesAsync(ct);
        return await details.GetAsync(content.Id, ct);
    }
}

/// <summary>Change a content's values (a new revision), by whoever the stage's editors are: its section, or the Desk ("edit as Desk").</summary>
public sealed record EditContentCommand(Guid Id, int Version, PostNode Values) : ICommand<Result<ContentDetail>>;

public sealed class EditContentHandler(ContentChanges changes, ContentAccess access, ContentPipelines pipelines)
    : ICommandHandler<EditContentCommand, Result<ContentDetail>>
{
    public async ValueTask<Result<ContentDetail>> Handle(EditContentCommand command, CancellationToken ct) =>
        await changes.ChangeAsync(command.Id, command.Version, async content =>
        {
            var stage = ContentPipelines.StageOf(await pipelines.CurrentAsync(ct), content);
            var allowed = await access.RequireAsync(content, stage.Editors, $"edit this content in stage '{stage.Key}'", ct);
            if (!allowed.IsSuccess) return allowed;
            content.Edit(access.Me, command.Values, stage, access.Now);
            return Result.Success();
        }, ct);
}

/// <summary>
/// Take a pipeline action on a content: <c>Act(contentId, action, expectedVersion, note?)</c>, checked against the tenant's
/// pipeline (the stage it starts from, who may take it, the note it needs). The Desk's accept pins the current revision.
/// An action into a review or accepted stage first has DynamicFields check the content (<see cref="ContentValidation"/>:
/// 422 with the problems, 503 while DynamicFields cannot answer). A key of the first built-in pipeline (<c>reject</c>) still
/// finds today's action (<c>return</c>).
/// </summary>
public sealed record ActOnContentCommand(Guid Id, string Action, int Version, string? Note) : ICommand<Result<ContentDetail>>;

public sealed class ActOnContentHandler(ContentChanges changes, ContentAccess access, ContentPipelines pipelines, ContentValidation validation)
    : ICommandHandler<ActOnContentCommand, Result<ContentDetail>>
{
    public async ValueTask<Result<ContentDetail>> Handle(ActOnContentCommand command, CancellationToken ct) =>
        await changes.ChangeAsync(command.Id, command.Version, async content =>
        {
            var pipeline = await pipelines.CurrentAsync(ct);
            var action = pipeline.FindAction(command.Action ?? "");
            if (action is null) return Result.Invalid(new ValidationError($"The pipeline has no action '{command.Action}'."));
            var allowed = await access.RequireAsync(content, action.Who, $"{action.Key} this content", ct);
            if (!allowed.IsSuccess) return allowed;
            if (!action.From.Contains(content.Stage)) // the aggregate's refusal, before DynamicFields is asked
                return Result.Conflict($"Cannot {action.Key} a content in stage '{content.Stage}'.");
            var valid = await validation.CheckContentAsync(content, pipeline, action, ct);
            if (!valid.IsSuccess) return valid;
            content.Act(access.Me, action, command.Note, access.Now);
            return Result.Success();
        }, ct);
}

/// <summary>
/// Give a content to a section and/or a person (the assign right in its category; only the workflow for the contents of its
/// posts while it owns assignment, <see cref="AssignmentRights"/>).
/// </summary>
public sealed record AssignContentCommand(Guid Id, int Version, string? SectionId, string? AssigneeId) : ICommand<Result<ContentDetail>>;

public sealed class AssignContentHandler(ContentChanges changes, ContentAccess access, AssignmentRights rights, IContentStore store)
    : ICommandHandler<AssignContentCommand, Result<ContentDetail>>
{
    public async ValueTask<Result<ContentDetail>> Handle(AssignContentCommand command, CancellationToken ct) =>
        await changes.ChangeAsync(command.Id, command.Version, async content =>
        {
            var managed = rights.WorkflowOwns && await store.IsWorkflowContentAsync(content.Id, content.OriginPostId, ct);
            var allowed = await rights.RequireAsync(managed, content.Scopes,
                () => access.Posts.RequireAsync(ContentPermissions.Assign, content.Scopes, "assign contents in this category", ct), ct);
            if (!allowed.IsSuccess) return allowed;
            content.Assign(access.Me, command.SectionId, command.AssigneeId, access.Now);
            return Result.Success();
        }, ct);
}

/// <summary>Take a content out of the library (Desk, or its creator): posts that use it keep it, no new post can.</summary>
public sealed record ArchiveContentCommand(Guid Id, int Version) : ICommand<Result<ContentDetail>>;

public sealed class ArchiveContentHandler(ContentChanges changes, ContentAccess access) : ICommandHandler<ArchiveContentCommand, Result<ContentDetail>>
{
    public async ValueTask<Result<ContentDetail>> Handle(ArchiveContentCommand command, CancellationToken ct) =>
        await changes.ChangeAsync(command.Id, command.Version, async content =>
        {
            var allowed = await access.RequireAsync(content, [PipelineCreator, ContentPermissions.Publish], "archive this content", ct);
            if (!allowed.IsSuccess) return allowed;
            content.Archive(access.Me, access.Now);
            return Result.Success();
        }, ct);

    internal const string PipelineCreator = Core.PipelineAggregate.PipelineDefinition.Creator;
}

/// <summary>Remove a content for good (Desk, or its creator); refused while any post uses it (archive it instead).</summary>
public sealed record DeleteContentCommand(Guid Id, int Version) : ICommand<Result>;

public sealed class DeleteContentHandler(IContentStore store, ContentAccess access) : ICommandHandler<DeleteContentCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteContentCommand command, CancellationToken ct)
    {
        var content = (await store.ContentsAsync([command.Id], ct)).FirstOrDefault();
        if (content is null || !await access.SeesAsync(content, ct)) return Result.NotFound($"Content {command.Id} was not found.");
        if (content.Version != command.Version)
            return Result.Conflict($"Content {command.Id} is at version {content.Version}, not {command.Version}; reload it and try again.");
        var allowed = await access.RequireAsync(content, [ArchiveContentHandler.PipelineCreator, ContentPermissions.Publish], "delete this content", ct);
        if (!allowed.IsSuccess) return allowed;
        if (await store.IsUsedAsync(content.Id, ct))
            return Result.Conflict("A post uses this content: it cannot be deleted while it is referenced (archive it instead).");
        content.Delete(access.Me, access.Now);
        store.Remove(content);
        await store.SaveChangesAsync(ct);
        return Result.Success();
    }
}
