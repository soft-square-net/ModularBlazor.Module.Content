using soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Contents;

/// <summary>A content in a list (the library, a post's contents tray).</summary>
public sealed record ContentSummary(Guid Id, string NodeId, string FieldKey, int FieldVersion, string PostKind, string PressId, string CategoryId,
    string? SectionId, string? AssigneeId, string CreatedBy, string Stage, int Revision, int? AcceptedRevision, int Version,
    DateTimeOffset UpdatedAt, bool Archived, Guid? UnitId)
{
    public static ContentSummary From(ContentItem c) => new(c.Id, c.NodeId, c.FieldKey, c.FieldVersion, c.PostKind, c.PressId, c.CategoryId,
        c.SectionId, c.AssigneeId, c.CreatedBy, c.Stage, c.Revision, c.AcceptedRevision, c.Version, c.UpdatedAt, c.IsArchived, c.UnitId);
}

/// <summary>
/// One content with its values, revisions and history. <see cref="Actions"/>: the pipeline actions the caller may take now;
/// <see cref="CanEdit"/>: whether the caller may change its values now. <see cref="UnitId"/>/<see cref="UnitPath"/>: the
/// Organization unit that works on it (set by the workflow), else <see cref="SectionId"/> (a Shifts section, legacy).
/// </summary>
public sealed record ContentDetail(Guid Id, string NodeId, string FieldKey, int FieldVersion, string PostKind, PostNode Values,
    string PressId, string CategoryId, string? OriginPostId, string? SectionId, string? AssigneeId, string CreatedBy,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string Stage, int Revision, int? AcceptedRevision, string? ReviewNote,
    DateTimeOffset? ArchivedAt, int Version, IReadOnlyList<ContentRevisionInfo> Revisions, IReadOnlyList<ContentHistoryDto> History,
    IReadOnlyList<string> Actions, bool CanEdit, Guid? UnitId, string? UnitPath);

/// <summary>One revision of a content, without its values (<c>GET /contents/{id}/revisions/{revision}</c> has them).</summary>
public sealed record ContentRevisionInfo(int Revision, string FieldKey, int FieldVersion, string By, DateTimeOffset At);

/// <summary>One revision of a content with its values.</summary>
public sealed record ContentRevisionDetail(Guid ContentId, int Revision, string FieldKey, int FieldVersion, PostNode Values, string By, DateTimeOffset At);

/// <summary>One entry of a content's audit trail.</summary>
public sealed record ContentHistoryDto(DateTimeOffset At, string Actor, string Action, string Stage, string? Detail);

/// <summary>
/// One content of a post, in reading order: the reference (required, pinned revision) and the content's state.
/// <see cref="Drift"/>: the Desk has accepted a newer revision than the one the post went live with.
/// </summary>
public sealed record PostContentDto(Guid ContentId, int Order, bool Required, int? PinnedRevision, string NodeId, string FieldKey,
    int FieldVersion, string Stage, string? SectionId, string? AssigneeId, int Revision, int? AcceptedRevision, string? ReviewNote,
    int Version, bool Archived, bool Drift, Guid? UnitId);
