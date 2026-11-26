namespace soft.square.net.WebApi.Modules.Content.Core.PostAggregate.Specifications;

/// <summary>A published Post addressed by its legacy permalink (<c>legacy-{id}</c>), with its slugs (its canonical path).</summary>
public sealed class PublishedPostByLegacyIdSpec : SingleResultSpecification<Post>
{
    public PublishedPostByLegacyIdSpec(int id) => Query.Where(p => p.Id == id && p.PublishedAt != null).Include(p => p.Slugs).AsNoTracking();
}
