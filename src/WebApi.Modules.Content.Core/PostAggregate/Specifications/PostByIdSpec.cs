namespace soft.square.net.WebApi.Modules.Content.Core.PostAggregate.Specifications;

/// <summary>The Post with this id, tracked (for a change); history is appended without loading it.</summary>
public sealed class PostByIdSpec : SingleResultSpecification<Post>
{
    public PostByIdSpec(int id) => Query.Where(a => a.Id == id).Include(a => a.ContentLinks).Include(a => a.Slugs);
}

/// <summary>The Post with this id alone, untracked (who may see it).</summary>
public sealed class PostByIdReadSpec : SingleResultSpecification<Post>
{
    public PostByIdReadSpec(int id) => Query.Where(a => a.Id == id).AsNoTracking();
}
