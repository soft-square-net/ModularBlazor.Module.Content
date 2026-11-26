using soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;
using soft.square.net.WebApi.Modules.Content.Core.FeedAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PipelineAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UseCases.Contents;

namespace soft.square.net.WebApi.Modules.Content.UnitTests.UseCases;

/// <summary>
/// <see cref="IContentStore"/> in memory. A save hands the raised feed events to the outbox, as the platform's
/// TenantDbContext does with the module's handlers.
/// </summary>
public sealed class FakeContentStore : IContentStore
{
    public List<ContentItem> Contents { get; } = [];
    public List<Post> Posts { get; } = [];
    public List<FeedEntry> Feed { get; } = [];
    public Pipeline? Pipeline { get; set; }
    public int Saves { get; private set; }

    public void Add(ContentItem content) => Contents.Add(content);
    public void Remove(ContentItem content) => Contents.Remove(content);
    public void Add(FeedEntry entry) => Feed.Add(entry);
    public void Add(Pipeline pipeline) => Pipeline = pipeline;

    public Task<IReadOnlyList<ContentItem>> ContentsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ContentItem>>(Contents.Where(c => ids.Contains(c.Id)).ToList());

    public Task<PostNode?> RevisionAsync(Guid contentId, int revision, CancellationToken ct) =>
        Task.FromResult(Contents.FirstOrDefault(c => c.Id == contentId)?.Revisions.FirstOrDefault(r => r.Revision == revision)?.Node);

    public Task<IReadOnlyList<Post>> PostsUsingAsync(Guid contentId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Post>>(Posts.Where(p => p.ContentLinks.Any(c => c.ContentId == contentId)).ToList());

    public Task<IReadOnlyList<(Guid ContentId, int PostId)>> PostIdsUsingAsync(IReadOnlyCollection<Guid> contentIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<(Guid ContentId, int PostId)>>(
            Posts.SelectMany(p => p.ContentLinks.Where(c => contentIds.Contains(c.ContentId)).Select(c => (c.ContentId, p.Id))).Distinct().ToList());

    public Task<IReadOnlyCollection<string>?> PostScopesAsync(int postId, CancellationToken ct) =>
        Task.FromResult(Posts.FirstOrDefault(p => p.Id == postId)?.Scopes);

    public Task<IReadOnlyList<ContentItem>> ContentsInSectionAsync(string sectionId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ContentItem>>(Contents.Where(c => c.SectionId == sectionId && c.UnitId == null).ToList());

    public Task<IReadOnlyList<Post>> PostsInUnitTreeAsync(string path, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Post>>(Posts.Where(p => p.UnitPath == path || (p.UnitPath?.StartsWith(path + "/") ?? false)).ToList());

    public Task<IReadOnlyList<ContentItem>> ContentsInUnitTreeAsync(string path, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ContentItem>>(Contents.Where(c => c.UnitPath == path || (c.UnitPath?.StartsWith(path + "/") ?? false)).ToList());

    public Task<bool> IsUsedAsync(Guid contentId, CancellationToken ct) => Task.FromResult(Posts.Any(p => p.ContentLinks.Any(c => c.ContentId == contentId)));

    public Task<Pipeline?> PipelineAsync(CancellationToken ct) => Task.FromResult(Pipeline);

    public Task<int?> SlugOwnerAsync(string culture, string slug, int exceptPostId, CancellationToken ct) =>
        Task.FromResult(Posts.Where(p => p.Id != exceptPostId && p.Slugs.Any(s => s.Culture == culture && s.Slug == slug)).Select(p => (int?)p.Id).FirstOrDefault());

    public Task<IReadOnlyList<FeedEntry>> FeedAsync(long after, DateTimeOffset notAfter, int take, int? postId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<FeedEntry>>(Feed.Take(take).ToList());

    public Task SaveChangesAsync(CancellationToken ct)
    {
        Saves++;
        foreach (var owner in Contents.Cast<IHasDomainEvents>().Concat(Posts))
            foreach (var e in owner.TakeDomainEvents().OfType<FeedEvent>())
                Feed.Add(FeedEntry.From(e));
        return Task.CompletedTask;
    }
}
