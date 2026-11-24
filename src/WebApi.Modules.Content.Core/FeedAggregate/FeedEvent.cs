namespace soft.square.net.WebApi.Modules.Content.Core.FeedAggregate;

/// <summary>The kinds of <see cref="FeedEvent"/>.</summary>
public static class FeedEventTypes
{
    /// <summary>A content was made (in the library, by a post's edit, or by the migration of a post).</summary>
    public const string ContentCreated = "content.created";
    /// <summary>A content's values changed: a new revision (<see cref="FeedEvent.Revision"/>).</summary>
    public const string ContentChanged = "content.changed";
    /// <summary>A content moved to another stage (<see cref="FeedEvent.Stage"/>); <see cref="FeedEvent.Detail"/> is the action and its note.</summary>
    public const string ContentStateChanged = "content.state-changed";
    /// <summary>A content got another section or assignee; <see cref="FeedEvent.Detail"/> is "section/assignee".</summary>
    public const string ContentAssigned = "content.assigned";
    /// <summary>A content left the library (posts that use it keep it).</summary>
    public const string ContentArchived = "content.archived";
    /// <summary>A content was removed for good (nothing referenced it).</summary>
    public const string ContentDeleted = "content.deleted";
    /// <summary>A post now uses a content (<see cref="FeedEvent.PostId"/> and <see cref="FeedEvent.ContentId"/>).</summary>
    public const string ContentAttached = "content.attached";
    /// <summary>A post no longer uses a content.</summary>
    public const string ContentDetached = "content.detached";
    /// <summary>A post's own values (title, root values), the order of its contents or its slugs changed.</summary>
    public const string PostChanged = "post.changed";
    /// <summary>A post moved to another workflow state (<see cref="FeedEvent.PostState"/>).</summary>
    public const string PostStateChanged = "post.state-changed";
}

/// <summary>
/// Raised by <c>Post</c> and <c>ContentItem</c>; a handler writes it to the outbox (<see cref="FeedEntry"/>) in the save that
/// raised it, so the feed never misses a change and never shows one that was rolled back.
/// </summary>
public sealed record FeedEvent(string Type, int? PostId, Guid? ContentId, int Version, int? Revision, string? Stage, string? PostState,
    string By, DateTimeOffset At, string? Detail = null) : IDomainEvent;

/// <summary>
/// One row of the module's outbox: the feed <c>GET /events?after=</c> that workflows (Elsa) read in order, and the per-post
/// feed <c>GET /posts/{id}/events?after=</c> the designer polls (the post's rows and those of the contents it uses).
/// Ids grow.
/// </summary>
public sealed class FeedEntry : EntityBase<long>, ITenantOwned
{
    public const int TypeLength = 40;
    public const int DetailLength = 500;

    private FeedEntry() { } // EF Core

    public string TenantId { get; set; } = "";
    public string Type { get; private set; } = "";
    public int? PostId { get; private set; }
    public Guid? ContentId { get; private set; }
    /// <summary>The version of the aggregate that raised it, after the change.</summary>
    public int Version { get; private set; }
    public int? Revision { get; private set; }
    public string? Stage { get; private set; }
    public string? PostState { get; private set; }
    public string By { get; private set; } = "";
    public DateTimeOffset At { get; private set; }
    public string? Detail { get; private set; }

    public static FeedEntry From(FeedEvent e) => new()
    {
        Type = e.Type,
        PostId = e.PostId,
        ContentId = e.ContentId,
        Version = e.Version,
        Revision = e.Revision,
        Stage = e.Stage,
        PostState = e.PostState,
        By = e.By,
        At = e.At,
        Detail = e.Detail is { Length: > DetailLength } text ? text[..DetailLength] : e.Detail
    };
}
