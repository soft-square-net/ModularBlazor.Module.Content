namespace soft.square.net.WebApi.Modules.Content.Core.ContentAggregate.Specifications;

/// <summary>One content with its revisions and history, untracked (for reading).</summary>
public sealed class ContentDetailSpec : SingleResultSpecification<ContentItem>
{
    public ContentDetailSpec(Guid id) => Query.Where(c => c.Id == id).Include(c => c.Revisions).Include(c => c.History).AsNoTracking();
}

/// <summary>Contents by id, untracked (a post's contents, for reading).</summary>
public sealed class ContentsByIdsSpec : Specification<ContentItem>
{
    public ContentsByIdsSpec(IReadOnlyCollection<Guid> ids) => Query.Where(c => ids.Contains(c.Id)).AsNoTracking();
}
