namespace soft.square.net.Content.Contracts;

/// <summary>
/// The integration events Content publishes (envelope source <c>content</c>; data camelCase JSON). The subject is
/// <c>post:{id}</c> for a post's events, <c>content:{id}</c> for a content's, and the work item's reference
/// (<see cref="ContentWorkItems.RefOf"/>) for the work-item events. The envelope's unit path is the post's or the content's.
/// Every event is written to the outbox in the save that makes the change, so none is sent for a change that was rolled back.
/// </summary>
public static class ContentEventTypes
{
    public const string Source = "content";

    // Posts.
    /// <summary><see cref="PostCreated"/>: a new post (a draft).</summary>
    public const string PostCreated = "content.post-created.v1";
    /// <summary><see cref="ContentFeedEvent"/> of feed type <c>post.changed</c>: its own values, the order of its contents or its slugs.</summary>
    public const string PostChanged = "content.post-changed.v1";
    /// <summary><see cref="ContentFeedEvent"/> of feed type <c>post.state-changed</c>: another workflow state (<c>PostState</c>).</summary>
    public const string PostStateChanged = "content.post-state-changed.v1";
    /// <summary><see cref="ContentFeedEvent"/> of feed type <c>content.attached</c>: a post now uses a content.</summary>
    public const string ContentAttached = "content.content-attached.v1";
    /// <summary><see cref="ContentFeedEvent"/> of feed type <c>content.detached</c>: a post no longer uses a content.</summary>
    public const string ContentDetached = "content.content-detached.v1";
    /// <summary><see cref="PostCategoriesChanged"/>.</summary>
    public const string PostCategoriesChanged = "content.post-categories-changed.v1";
    /// <summary><see cref="PostUnitChanged"/>.</summary>
    public const string PostUnitChanged = "content.post-unit-changed.v1";

    // Contents of the shared library.
    /// <summary><see cref="ContentFeedEvent"/> of feed type <c>content.created</c>.</summary>
    public const string ContentCreated = "content.content-created.v1";
    /// <summary><see cref="ContentFeedEvent"/> of feed type <c>content.changed</c>: a new revision of its values.</summary>
    public const string ContentChanged = "content.content-changed.v1";
    /// <summary><see cref="ContentFeedEvent"/> of feed type <c>content.state-changed</c>: another pipeline stage.</summary>
    public const string ContentStateChanged = "content.content-state-changed.v1";
    /// <summary><see cref="ContentFeedEvent"/> of feed type <c>content.assigned</c>: another section, unit or assignee.</summary>
    public const string ContentAssigned = "content.content-assigned.v1";
    /// <summary><see cref="ContentFeedEvent"/> of feed type <c>content.archived</c>.</summary>
    public const string ContentArchived = "content.content-archived.v1";
    /// <summary><see cref="ContentFeedEvent"/> of feed type <c>content.deleted</c>.</summary>
    public const string ContentDeleted = "content.content-deleted.v1";
    /// <summary><see cref="ContentValidationFailed"/>: DynamicFields refused a content or a post (the action was refused with 422).</summary>
    public const string ValidationFailed = "content.validation-failed.v1";

    // Work items (the platform's WorkItemAssigned / WorkItemStateChanged records, WorkItemSnapshot inside).
    /// <summary>The platform's <c>WorkItemAssigned</c>: a content got an assignee.</summary>
    public const string WorkItemAssigned = "content.work-item-assigned.v1";
    /// <summary>The platform's <c>WorkItemStateChanged</c>: a content moved to another stage (state = stage key).</summary>
    public const string WorkItemStateChanged = "content.work-item-state-changed.v1";

    /// <summary>
    /// The integration type of each feed type (<c>GET /events</c>' <c>type</c>): the feed rows are mapped one to one onto the
    /// outbox, with <see cref="ContentFeedEvent"/> as their data. <c>content.validation-failed</c> is not in the map: it has
    /// its own record (<see cref="ValidationFailed"/>).
    /// </summary>
    public static IReadOnlyDictionary<string, string> OfFeedType { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["content.created"] = ContentCreated,
        ["content.changed"] = ContentChanged,
        ["content.state-changed"] = ContentStateChanged,
        ["content.assigned"] = ContentAssigned,
        ["content.archived"] = ContentArchived,
        ["content.deleted"] = ContentDeleted,
        ["content.attached"] = ContentAttached,
        ["content.detached"] = ContentDetached,
        ["post.changed"] = PostChanged,
        ["post.state-changed"] = PostStateChanged
    };

    /// <summary>Every type Content publishes.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        PostCreated, PostChanged, PostStateChanged, ContentAttached, ContentDetached, PostCategoriesChanged, PostUnitChanged,
        ContentCreated, ContentChanged, ContentStateChanged, ContentAssigned, ContentArchived, ContentDeleted, ValidationFailed,
        WorkItemAssigned, WorkItemStateChanged
    ];

    /// <summary><c>post:{postId}</c>.</summary>
    public static string PostSubject(int postId) => $"post:{postId.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    /// <summary><c>content:{contentId}</c>.</summary>
    public static string ContentSubject(Guid contentId) => $"content:{contentId:D}";
}

/// <summary>Data of <c>content.post-created.v1</c>: the new post (a draft) and how it is filed.</summary>
public sealed record PostCreated(int PostId, string PublicId, int Version, string PostKind, string Title, string OwnerId, string State,
    string PressId, string PrimaryCategoryId, IReadOnlyList<string> Categories, IReadOnlyList<Guid> Contents, Guid? UnitId, string? UnitPath,
    DateTimeOffset CreatedAt);

/// <summary>Data of <c>content.post-categories-changed.v1</c>: the post's categories now (primary first) and before.</summary>
public sealed record PostCategoriesChanged(int PostId, string PublicId, int Version, string PressId, string PrimaryCategoryId,
    IReadOnlyList<string> Categories, IReadOnlyList<string> PreviousCategories, Guid? UnitId, string? UnitPath);

/// <summary>Data of <c>content.post-unit-changed.v1</c>: the post's unit now (null: none) and before.</summary>
public sealed record PostUnitChanged(int PostId, string PublicId, int Version, string PressId, string PrimaryCategoryId,
    IReadOnlyList<string> Categories, Guid? UnitId, string? UnitPath, Guid? PreviousUnitId);

/// <summary>
/// Data of the events mapped from the feed (<see cref="ContentEventTypes.OfFeedType"/>): the feed row as <c>GET /events</c>
/// serves it, without its id. <see cref="FeedType"/> is the row's type; <see cref="Version"/> the version of the post or
/// content after the change.
/// </summary>
public sealed record ContentFeedEvent(string FeedType, int? PostId, Guid? ContentId, int Version, int? Revision, string? Stage,
    string? PostState, string By, DateTimeOffset At, string? Detail);

/// <summary>One problem DynamicFields found: a JSON Pointer into what was checked, its message, and the node it is in.</summary>
public sealed record ContentProblem(string Path, string Message, string? NodeId);

/// <summary>
/// Data of <c>content.validation-failed.v1</c>. <see cref="Check"/> is <c>node</c> (a content's action into a review or
/// accepted stage: DynamicFields' <c>validate-node</c>) or <c>post</c> (a post's approve, schedule or publish: <c>validate</c>).
/// <see cref="Action"/> is the pipeline action or the post step that was refused.
/// </summary>
public sealed record ContentValidationFailed(string Check, string Action, Guid? ContentId, int? PostId, int Version,
    IReadOnlyList<ContentProblem> Problems, string By, DateTimeOffset At)
{
    public const string NodeCheck = "node";
    public const string PostCheck = "post";
}
