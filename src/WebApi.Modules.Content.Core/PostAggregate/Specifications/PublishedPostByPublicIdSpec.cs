namespace soft.square.net.WebApi.Modules.Content.Core.PostAggregate.Specifications;

/// <summary>
/// A Post with a live published snapshot. Matched on <see cref="Post.PublishedAt"/>, not the current state: while a later
/// edit waits for Desk re-approval (or was returned), the previously published snapshot stays live.
/// </summary>
public sealed class PublishedPostByPublicIdSpec : SingleResultSpecification<Post>
{
    public PublishedPostByPublicIdSpec(string publicId) => Query.Where(p => p.PublicId == publicId && p.PublishedAt != null).AsNoTracking();
}
