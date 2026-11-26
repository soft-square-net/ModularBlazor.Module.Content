using Microsoft.Extensions.Options;
using soft.square.net.WebApi.Modules.Content.Core.FeedAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PipelineAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate.Specifications;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Contents;

/// <summary>The tenant's pipeline as Content applies it: the published one (<see cref="Version"/> &gt; 0) or the default (0).</summary>
public sealed record PipelineInfo(int Version, DateTimeOffset? UpdatedAt, string? UpdatedBy, PipelineDefinition Definition);

/// <summary>The content pipeline in force (any signed-in caller: the studio shows stages and actions from it).</summary>
public sealed record GetPipelineQuery : IQuery<Result<PipelineInfo>>;

public sealed class GetPipelineHandler(IContentStore store, PostAccess access) : IQueryHandler<GetPipelineQuery, Result<PipelineInfo>>
{
    public async ValueTask<Result<PipelineInfo>> Handle(GetPipelineQuery query, CancellationToken ct)
    {
        _ = access.Me;
        var pipeline = await store.PipelineAsync(ct);
        return pipeline is null
            ? new PipelineInfo(0, null, null, PipelineDefinition.Default)
            : new PipelineInfo(pipeline.Version, pipeline.UpdatedAt, pipeline.UpdatedBy, pipeline.Definition);
    }
}

/// <summary>
/// Publish a new pipeline (Elsa, or an admin: <c>content.pipeline</c> tenant-wide). It applies to the next action; contents
/// keep their stage keys. The stage keys of the first built-in pipeline are renamed to today's
/// (<see cref="PipelineDefinition.WithCurrentStageKeys"/>: draft → assigned, desk → submitted, rejected → returned), as the
/// migration renamed the stored stages.
/// </summary>
public sealed record PublishPipelineCommand(PipelineDefinition Definition) : ICommand<Result<PipelineInfo>>;

public sealed class PublishPipelineHandler(IContentStore store, PostAccess access) : ICommandHandler<PublishPipelineCommand, Result<PipelineInfo>>
{
    public async ValueTask<Result<PipelineInfo>> Handle(PublishPipelineCommand command, CancellationToken ct)
    {
        var allowed = await access.RequireTenantWideAsync(ContentPermissions.Pipeline, "Publishing the pipeline needs content.pipeline for the whole tenant.", ct);
        if (!allowed.IsSuccess) return allowed.As<PipelineInfo>();
        if (command.Definition is null) return Result<PipelineInfo>.Invalid(new ValidationError("A pipeline definition is required."));
        var definition = command.Definition.WithCurrentStageKeys();
        var pipeline = await store.PipelineAsync(ct);
        if (pipeline is null) store.Add(Pipeline.Create(definition, access.Me, access.Now));
        else pipeline.Publish(definition, access.Me, access.Now);
        await store.SaveChangesAsync(ct);
        var saved = (await store.PipelineAsync(ct))!;
        return new PipelineInfo(saved.Version, saved.UpdatedAt, saved.UpdatedBy, saved.Definition);
    }
}

/// <summary>One event of the feed. <see cref="Type"/> is one of <see cref="FeedEventTypes"/>.</summary>
public sealed record FeedEventDto(long Id, string Type, int? PostId, Guid? ContentId, int Version, int? Revision, string? Stage,
    string? PostState, string By, DateTimeOffset At, string? Detail)
{
    public static FeedEventDto From(FeedEntry e) =>
        new(e.Id, e.Type, e.PostId, e.ContentId, e.Version, e.Revision, e.Stage, e.PostState, e.By, e.At, e.Detail);
}

/// <summary>A page of the feed: read the next one with <c>after = Last</c> (the same value again when there was nothing new).</summary>
public sealed record FeedPage(IReadOnlyList<FeedEventDto> Events, long Last, bool More);

/// <summary>
/// The tenant's events after <see cref="After"/>, oldest first: what workflows (Elsa) poll to react to posts and contents
/// (<c>content.events</c> tenant-wide). The newest seconds (<c>Content:EventDelay</c>) are held back, so a slower save cannot
/// land behind an id a reader already passed.
/// </summary>
public sealed record GetEventsQuery(long After, int? Take) : IQuery<Result<FeedPage>>;

/// <summary>One post's events after <see cref="After"/>: its own and those of the contents it uses (the studio's hot reload).</summary>
public sealed record GetPostEventsQuery(int PostId, long After, int? Take) : IQuery<Result<FeedPage>>;

public sealed class FeedReader(IContentStore store, TimeProvider clock, IOptions<ContentOptions> options)
{
    public const int MaxTake = 500;

    public async Task<FeedPage> ReadAsync(long after, int? take, int? postId, CancellationToken ct)
    {
        var size = Math.Clamp(take ?? 100, 1, MaxTake);
        after = Math.Max(0, after);
        var events = await store.FeedAsync(after, clock.GetUtcNow() - options.Value.EventDelay, size + 1, postId, ct);
        var page = events.Take(size).Select(FeedEventDto.From).ToList();
        return new FeedPage(page, page.Count > 0 ? page[^1].Id : after, events.Count > size);
    }
}

public sealed class GetEventsHandler(FeedReader feed, PostAccess access) : IQueryHandler<GetEventsQuery, Result<FeedPage>>
{
    public async ValueTask<Result<FeedPage>> Handle(GetEventsQuery query, CancellationToken ct)
    {
        var allowed = await access.RequireTenantWideAsync(ContentPermissions.Events, "Reading the feed needs content.events for the whole tenant.", ct);
        if (!allowed.IsSuccess) return allowed.As<FeedPage>();
        return await feed.ReadAsync(query.After, query.Take, null, ct);
    }
}

public sealed class GetPostEventsHandler(FeedReader feed, PostDetails details) : IQueryHandler<GetPostEventsQuery, Result<FeedPage>>
{
    public async ValueTask<Result<FeedPage>> Handle(GetPostEventsQuery query, CancellationToken ct)
    {
        if (!await details.SeesAsync(query.PostId, ct)) return Result<FeedPage>.NotFound($"Post {query.PostId} was not found.");
        return await feed.ReadAsync(query.After, query.Take, query.PostId, ct);
    }
}

/// <summary>Writes every <see cref="FeedEvent"/> to the outbox, in the save that raised it.</summary>
public sealed class FeedWriter(IContentStore store) : IDomainEventHandler<FeedEvent>
{
    public Task HandleAsync(FeedEvent domainEvent, CancellationToken cancellationToken)
    {
        store.Add(FeedEntry.From(domainEvent));
        return Task.CompletedTask;
    }
}

/// <summary>
/// When a content's values change, every post that uses it gets its working document refreshed in the same save (the
/// published snapshots never change: they keep their pinned revisions).
/// </summary>
public sealed class RecomposePostsOnContentChange(IContentStore store, PostContents contents) : IDomainEventHandler<FeedEvent>
{
    public async Task HandleAsync(FeedEvent domainEvent, CancellationToken cancellationToken)
    {
        if (domainEvent.Type != FeedEventTypes.ContentChanged || domainEvent.ContentId is not { } id) return;
        foreach (var post in await store.PostsUsingAsync(id, cancellationToken))
            if (post.ContentLinks.Any(c => c.ContentId == id))
                await contents.RecomposeAsync(post, cancellationToken);
    }
}
