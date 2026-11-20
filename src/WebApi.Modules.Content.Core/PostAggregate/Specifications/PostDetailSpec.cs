namespace soft.square.net.WebApi.Modules.Content.Core.PostAggregate.Specifications;

/// <summary>The Post with its history, untracked (for reading).</summary>
public sealed class PostDetailSpec : SingleResultSpecification<Post>
{
    public PostDetailSpec(int id) => Query.Where(a => a.Id == id).Include(a => a.History).AsNoTracking();
}
