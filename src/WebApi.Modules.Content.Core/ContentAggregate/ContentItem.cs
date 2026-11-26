using System.Text.Json;
using soft.square.net.WebApi.Modules.Content.Core.FeedAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PipelineAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;

/// <summary>
/// One content of the shared library (aggregate root): a paragraph, heading, image, video, gallery, quote, ... of a
/// DynamicFields block field (<see cref="FieldKey"/> + <see cref="FieldVersion"/>). Several posts can use it; each post pins
/// the Desk-accepted revision it publishes.
/// <list type="bullet">
/// <item><see cref="Revision"/> counts its values (every edit adds one to <see cref="Revisions"/>, the version history);
/// <see cref="Version"/> counts every change, values or not, and is the optimistic concurrency token callers send back.</item>
/// <item><see cref="Stage"/> is a key of the tenant's pipeline (data, published by Elsa), never an enum: the pipeline says
/// which actions move it and who may take them; the caller checks the who, this aggregate the rest.</item>
/// <item>A section (Shifts) and an assignee work on it; the Desk reviews it: accept (pins <see cref="AcceptedRevision"/>),
/// reject with a note, or edit as Desk.</item>
/// </list>
/// Its values are one node (<see cref="PostNode"/>): nested nodes (a gallery's images) stay inside it.
/// </summary>
public sealed class ContentItem : AggregateRoot<Guid>, ITenantOwned
{
    public const int NodeIdLength = 64;
    public const int StageLength = PipelineDefinition.MaxKeyLength;
    public const int NoteLength = 1000;

    private readonly List<ContentRevision> _revisions = [];
    private readonly List<ContentHistoryEntry> _history = [];

    private ContentItem() { } // EF Core

    public string TenantId { get; set; } = "";
    /// <summary>The node id it has in every assembled document (translations are keyed by it). Never changes.</summary>
    public string NodeId { get; private set; } = "";
    public string FieldKey { get; private set; } = "";
    public int FieldVersion { get; private set; }
    /// <summary>The kind of the post it was made for (a root field key, <see cref="PostKinds"/>), from its node.</summary>
    public string PostKind { get; private set; } = PostKinds.Article;
    /// <summary>The current values: the node (<see cref="PostNode"/>) as JSON.</summary>
    public string ValuesJson { get; private set; } = "{}";
    public int Revision { get; private set; }
    public int Version { get; private set; }

    public string PressId { get; private set; } = "";
    public string CategoryId { get; private set; } = "";
    /// <summary>The public id of the post it was made for, if any: only that post's whole-document edit may change it.</summary>
    public string? OriginPostId { get; private set; }
    public string? SectionId { get; private set; }
    public string? AssigneeId { get; private set; }
    public string CreatedBy { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public string Stage { get; private set; } = "";
    /// <summary>The revision the Desk accepted last; null until then. Posts publish this one.</summary>
    public int? AcceptedRevision { get; private set; }
    /// <summary>The Desk's note with the last rejection (cleared by the next accept).</summary>
    public string? ReviewNote { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }
    public bool IsArchived => ArchivedAt is not null;

    public IReadOnlyList<ContentRevision> Revisions => _revisions;
    public IReadOnlyList<ContentHistoryEntry> History => _history;

    /// <summary>Its grant scopes: the press and category it was made in.</summary>
    public IReadOnlyCollection<string> Scopes => [$"press:{PressId}", $"category:{CategoryId}"];

    /// <summary>The current values as a document node.</summary>
    public PostNode Node => ReadNode(ValuesJson);

    public static PostNode ReadNode(string json) =>
        JsonSerializer.Deserialize<PostNode>(json, PostJson.Options) ?? throw new DomainException("A content has invalid values.");

    /// <summary>A new content at revision 1, in the pipeline's start stage. The node's own id is kept when given (a post's node).</summary>
    public static ContentItem Create(string actor, string pressId, string categoryId, string? originPostId, PostNode values, string startStage,
        DateTimeOffset now, string? sectionId = null, string? assigneeId = null)
    {
        var content = new ContentItem
        {
            Id = Guid.NewGuid(),
            PressId = Require(pressId, "a press"),
            CategoryId = Require(categoryId, "a category"),
            OriginPostId = originPostId,
            CreatedBy = Require(actor, "a creator"),
            CreatedAt = now,
            UpdatedAt = now,
            Stage = Require(startStage, "a stage"),
            SectionId = Optional(sectionId),
            AssigneeId = Optional(assigneeId)
        };
        content.NodeId = string.IsNullOrWhiteSpace(values.NodeId) ? $"c-{content.Id:N}"[..14] : values.NodeId.Trim();
        content.SetValues(actor, values, now);
        content.Record(actor, "created", now, null);
        content.Emit(FeedEventTypes.ContentCreated, actor, now);
        return content;
    }

    /// <summary>
    /// A content split from an existing post by the migration: revision 1 in the given stage, accepted (and so pinnable)
    /// when <paramref name="accepted"/>. Raises nothing: the migration is not news for the feed.
    /// </summary>
    public static ContentItem Migrate(Post post, PostNode node, string stage, bool accepted, DateTimeOffset now)
    {
        var content = new ContentItem
        {
            Id = Guid.NewGuid(),
            NodeId = node.NodeId,
            PressId = post.PressId,
            CategoryId = post.CategoryId,
            OriginPostId = post.StablePublicId,
            CreatedBy = post.OwnerId,
            CreatedAt = post.CreatedAt,
            UpdatedAt = now,
            Stage = stage
        };
        content.SetValues(post.OwnerId, node, now);
        if (accepted) content.AcceptedRevision = content.Revision;
        content.Record("system", "migrated", now, $"from post {post.Id}");
        content.TakeEvents();
        return content;
    }

    /// <summary>
    /// New values (a new revision), unless they are the same as now (false). In a stage with an <c>OnEdit</c> stage the content
    /// moves there (an accepted content goes back to the Desk; the accepted revision stays pinned until accepted again).
    /// Who may edit in the current stage is the caller's check (<see cref="PipelineStage.Editors"/>).
    /// </summary>
    public bool Edit(string actor, PostNode values, PipelineStage current, DateTimeOffset now)
    {
        RequireActive("edit");
        if (current.Key != Stage) throw new DomainException($"The content is in stage '{Stage}', not '{current.Key}'.");
        var node = values with { NodeId = NodeId };
        if (JsonSerializer.Serialize(node, PostJson.Options) == ValuesJson) return false;
        SetValues(actor, node, now);
        Change(actor, "edited", now, $"revision {Revision}");
        Emit(FeedEventTypes.ContentChanged, actor, now);
        if (current.OnEdit is { } next && next != Stage) MoveTo(actor, "edited", next, null, now);
        return true;
    }

    /// <summary>
    /// Takes a pipeline action: it must start from the current stage, and have its note when it needs one. A pinning action
    /// accepts the current revision. Who may take it is the caller's check (<see cref="PipelineAction.Who"/>).
    /// </summary>
    public void Act(string actor, PipelineAction action, string? note, DateTimeOffset now)
    {
        RequireActive(action.Key);
        if (!action.From.Contains(Stage)) throw new DomainException($"Cannot {action.Key} a content in stage '{Stage}'.");
        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (action.RequiresNote && note is null) throw new DomainException($"'{action.Key}' needs a note.");
        if (note is { Length: > NoteLength }) throw new DomainException($"A note has at most {NoteLength} characters.");
        if (action.Pins)
        {
            AcceptedRevision = Revision;
            ReviewNote = null;
        }
        else if (action.RequiresNote) ReviewNote = note;
        MoveTo(actor, action.Key, action.To, note, now);
    }

    public void Assign(string actor, string? sectionId, string? assigneeId, DateTimeOffset now)
    {
        RequireActive("assign");
        SectionId = Optional(sectionId);
        AssigneeId = Optional(assigneeId);
        var detail = $"{SectionId}/{AssigneeId}";
        Change(actor, "assigned", now, detail);
        Emit(FeedEventTypes.ContentAssigned, actor, now, detail);
    }

    /// <summary>Takes it out of the library: no new post can use it and nobody works on it; the posts that use it keep it.</summary>
    public void Archive(string actor, DateTimeOffset now)
    {
        RequireActive("archive");
        Change(actor, "archived", now, null, () => ArchivedAt = now);
        Emit(FeedEventTypes.ContentArchived, actor, now);
    }

    /// <summary>Records the removal in the feed; the caller has checked that no post references it, then removes it.</summary>
    public void Delete(string actor, DateTimeOffset now) => Emit(FeedEventTypes.ContentDeleted, actor, now);

    /// <summary>Who may act or edit: the assignee (the creator while none), the creator, or a permission checked by the caller.</summary>
    public bool Matches(string token, string user) => token switch
    {
        PipelineDefinition.Assignee => (AssigneeId ?? CreatedBy) == user,
        PipelineDefinition.Creator => CreatedBy == user,
        _ => false
    };

    private void SetValues(string actor, PostNode node, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(node.SchemaId)) throw new DomainException("A content needs its field (schema id).");
        node = node with { NodeId = NodeId };
        PostDocumentRules.Validate(node);
        FieldKey = node.SchemaId.Trim();
        FieldVersion = node.SchemaVersion;
        PostKind = node.PostKind;
        ValuesJson = JsonSerializer.Serialize(node, PostJson.Options);
        Revision++;
        _revisions.Add(new ContentRevision(Revision, FieldKey, FieldVersion, ValuesJson, actor, now));
    }

    private void MoveTo(string actor, string action, string stage, string? note, DateTimeOffset now)
    {
        var from = Stage;
        Change(actor, action, now, note is null ? $"{from} → {stage}" : $"{from} → {stage}: {note}", () => Stage = stage);
        Emit(FeedEventTypes.ContentStateChanged, actor, now, note is null ? action : $"{action}: {note}");
    }

    private void RequireActive(string what)
    {
        if (IsArchived) throw new DomainException($"Cannot {what} an archived content.");
    }

    private void Change(string actor, string action, DateTimeOffset now, string? detail, Action? apply = null)
    {
        apply?.Invoke();
        Version++;
        UpdatedAt = now;
        Record(actor, action, now, detail);
    }

    private void Record(string actor, string action, DateTimeOffset now, string? detail) =>
        _history.Add(new ContentHistoryEntry(now, actor, action, Stage, detail is { Length: > NoteLength + 100 } d ? d[..(NoteLength + 100)] : detail));

    private void Emit(string type, string actor, DateTimeOffset now, string? detail = null) =>
        Raise(new FeedEvent(type, null, Id, Version, Revision, Stage, null, actor, now, detail));

    private void TakeEvents() => ((IHasDomainEvents)this).TakeDomainEvents();

    private static string Require(string value, string what) =>
        !string.IsNullOrWhiteSpace(value) ? value.Trim() : throw new DomainException($"A content needs {what}.");

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>One revision of a content's values (the version history). Append-only.</summary>
public sealed class ContentRevision : EntityBase<int>
{
    private ContentRevision() { } // EF Core

    public ContentRevision(int revision, string fieldKey, int fieldVersion, string valuesJson, string by, DateTimeOffset at)
    {
        Revision = revision;
        FieldKey = fieldKey;
        FieldVersion = fieldVersion;
        ValuesJson = valuesJson;
        By = by;
        At = at;
    }

    public Guid ContentId { get; private set; }
    public int Revision { get; private set; }
    public string FieldKey { get; private set; } = "";
    public int FieldVersion { get; private set; }
    public string ValuesJson { get; private set; } = "{}";
    public string By { get; private set; } = "";
    public DateTimeOffset At { get; private set; }

    public PostNode Node => ContentItem.ReadNode(ValuesJson);
}

/// <summary>One entry of a content's audit trail: who did what, when, and the stage it led to.</summary>
public sealed class ContentHistoryEntry : EntityBase<int>
{
    private ContentHistoryEntry() { } // EF Core

    public ContentHistoryEntry(DateTimeOffset at, string actor, string action, string stage, string? detail)
    {
        At = at;
        Actor = actor;
        Action = action;
        Stage = stage;
        Detail = detail;
    }

    public DateTimeOffset At { get; private set; }
    public string Actor { get; private set; } = "";
    public string Action { get; private set; } = "";
    public string Stage { get; private set; } = "";
    public string? Detail { get; private set; }
}
