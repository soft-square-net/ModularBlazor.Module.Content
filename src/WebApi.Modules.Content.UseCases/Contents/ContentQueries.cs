using System.Linq.Expressions;
using soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;
using soft.square.net.WebApi.Modules.Content.Core.ContentAggregate.Specifications;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Contents;

/// <summary>Reads one content as the caller may see it, with the actions the caller may take now.</summary>
public sealed class ContentDetails(IReadRepository<ContentItem> reader, ContentAccess access, ContentPipelines pipelines)
{
    public async Task<Result<ContentDetail>> GetAsync(Guid id, CancellationToken ct)
    {
        var content = await reader.FirstOrDefaultAsync(new ContentDetailSpec(id), ct);
        if (content is null || !await access.SeesAsync(content, ct)) return NotFound(id);
        var pipeline = await pipelines.CurrentAsync(ct);
        var actions = new List<string>();
        if (!content.IsArchived)
            foreach (var action in pipeline.Actions.Where(a => a.From.Contains(content.Stage)))
                if (await access.MatchesAsync(content, action.Who, ct)) actions.Add(action.Key);
        var canEdit = !content.IsArchived && await access.MatchesAsync(content, ContentPipelines.StageOf(pipeline, content).Editors, ct);
        return new ContentDetail(content.Id, content.NodeId, content.FieldKey, content.FieldVersion, content.PostKind, content.Node,
            content.PressId, content.CategoryId, content.OriginPostId, content.SectionId, content.AssigneeId, content.CreatedBy,
            content.CreatedAt, content.UpdatedAt, content.Stage, content.Revision, content.AcceptedRevision, content.ReviewNote,
            content.ArchivedAt, content.Version,
            content.Revisions.OrderBy(r => r.Revision).Select(r => new ContentRevisionInfo(r.Revision, r.FieldKey, r.FieldVersion, r.By, r.At)).ToList(),
            content.History.OrderBy(h => h.At).ThenBy(h => h.Id).Select(h => new ContentHistoryDto(h.At, h.Actor, h.Action, h.Stage, h.Detail)).ToList(),
            actions, canEdit);
    }

    public static Result<ContentDetail> NotFound(Guid id) => Result<ContentDetail>.NotFound($"Content {id} was not found.");
}

/// <summary>One content with its revisions, history and the caller's actions.</summary>
public sealed record GetContentQuery(Guid Id) : IQuery<Result<ContentDetail>>;

public sealed class GetContentHandler(ContentDetails details) : IQueryHandler<GetContentQuery, Result<ContentDetail>>
{
    public async ValueTask<Result<ContentDetail>> Handle(GetContentQuery query, CancellationToken ct) => await details.GetAsync(query.Id, ct);
}

/// <summary>One revision of a content, with its values.</summary>
public sealed record GetContentRevisionQuery(Guid Id, int Revision) : IQuery<Result<ContentRevisionDetail>>;

public sealed class GetContentRevisionHandler(IReadRepository<ContentItem> reader, ContentAccess access)
    : IQueryHandler<GetContentRevisionQuery, Result<ContentRevisionDetail>>
{
    public async ValueTask<Result<ContentRevisionDetail>> Handle(GetContentRevisionQuery query, CancellationToken ct)
    {
        var content = await reader.FirstOrDefaultAsync(new ContentDetailSpec(query.Id), ct);
        if (content is null || !await access.SeesAsync(content, ct)) return Result<ContentRevisionDetail>.NotFound($"Content {query.Id} was not found.");
        var revision = content.Revisions.FirstOrDefault(r => r.Revision == query.Revision);
        if (revision is null) return Result<ContentRevisionDetail>.NotFound($"Content {query.Id} has no revision {query.Revision}.");
        return new ContentRevisionDetail(content.Id, revision.Revision, revision.FieldKey, revision.FieldVersion, revision.Node, revision.By, revision.At);
    }
}

/// <summary>
/// The library: the contents the caller may see (made by or assigned to them, or in a press or category where they hold
/// a content permission), newest change first, filtered. Archived ones only when asked.
/// </summary>
public sealed record ListContentsQuery(string? SectionId = null, string? Stage = null, string? AssigneeId = null, string? FieldKey = null,
    bool Archived = false, int Skip = 0, int Take = 50) : IQuery<Result<IReadOnlyList<ContentSummary>>>;

public sealed class ListContentsHandler(IReadRepository<ContentItem> reader, ContentAccess access)
    : IQueryHandler<ListContentsQuery, Result<IReadOnlyList<ContentSummary>>>
{
    public const int MaxTake = 200;

    public async ValueTask<Result<IReadOnlyList<ContentSummary>>> Handle(ListContentsQuery query, CancellationToken ct)
    {
        var me = access.Me;
        var (tenant, presses, categories) = await access.Posts.WhereAsync(
            [.. Posts.PostAccess.SeeingPermissions, ContentPermissions.Author, ContentPermissions.Review], ct);
        Expression<Func<ContentItem, bool>> visible = tenant
            ? c => true
            : c => c.CreatedBy == me || c.AssigneeId == me || presses.Contains(c.PressId) || categories.Contains(c.CategoryId);
        var contents = await reader.ListAsync(new ContentListSpec(visible, query with { Take = Math.Clamp(query.Take, 1, MaxTake) }), ct);
        return (contents ?? []).Select(ContentSummary.From).ToList();
    }
}

public sealed class ContentListSpec : Specification<ContentItem>
{
    public ContentListSpec(Expression<Func<ContentItem, bool>> visible, ListContentsQuery q)
    {
        Query.Where(visible).Where(c => q.Archived ? c.ArchivedAt != null : c.ArchivedAt == null);
        if (!string.IsNullOrWhiteSpace(q.SectionId)) Query.Where(c => c.SectionId == q.SectionId);
        if (!string.IsNullOrWhiteSpace(q.Stage)) Query.Where(c => c.Stage == q.Stage);
        if (!string.IsNullOrWhiteSpace(q.AssigneeId)) Query.Where(c => c.AssigneeId == q.AssigneeId);
        if (!string.IsNullOrWhiteSpace(q.FieldKey)) Query.Where(c => c.FieldKey == q.FieldKey);
        Query.OrderByDescending(c => c.UpdatedAt).ThenBy(c => c.Id).Skip(Math.Max(0, q.Skip)).Take(q.Take).AsNoTracking();
    }
}
