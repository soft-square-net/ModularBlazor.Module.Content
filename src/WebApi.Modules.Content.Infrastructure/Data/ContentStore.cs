using Microsoft.EntityFrameworkCore;
using soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;
using soft.square.net.WebApi.Modules.Content.Core.FeedAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PipelineAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UseCases.Contents;

namespace soft.square.net.WebApi.Modules.Content.Infrastructure.Data;

/// <summary><see cref="IContentStore"/> on the request's <see cref="ContentDbContext"/> (the current tenant's database).</summary>
public sealed class ContentStore(ContentDbContext db) : IContentStore
{
    public void Add(ContentItem content) => db.Contents.Add(content);

    public void Remove(ContentItem content) => db.Contents.Remove(content);

    public void Add(FeedEntry entry) => db.Events.Add(entry);

    public void Add(Pipeline pipeline) => db.Pipelines.Add(pipeline);

    public async Task<IReadOnlyList<ContentItem>> ContentsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        // Contents added to this save are not in the database yet.
        var local = db.Contents.Local.Where(c => ids.Contains(c.Id)).ToList();
        var rest = ids.Except(local.Select(c => c.Id)).ToList();
        if (rest.Count == 0) return local;
        return [.. local, .. await db.Contents.Where(c => rest.Contains(c.Id)).ToListAsync(ct)];
    }

    public async Task<PostNode?> RevisionAsync(Guid contentId, int revision, CancellationToken ct)
    {
        var json = await db.ContentRevisions.Where(r => r.ContentId == contentId && r.Revision == revision).Select(r => r.ValuesJson)
            .FirstOrDefaultAsync(ct);
        return json is null ? null : ContentItem.ReadNode(json);
    }

    public async Task<IReadOnlyList<Post>> PostsUsingAsync(Guid contentId, CancellationToken ct) =>
        await db.Posts.Where(p => p.ContentLinks.Any(c => c.ContentId == contentId)).Include(p => p.ContentLinks).ToListAsync(ct);

    public Task<bool> IsUsedAsync(Guid contentId, CancellationToken ct) => db.PostContents.AnyAsync(c => c.ContentId == contentId, ct);

    public Task<Pipeline?> PipelineAsync(CancellationToken ct) => db.Pipelines.OrderBy(p => p.Id).FirstOrDefaultAsync(ct);

    public async Task<int?> SlugOwnerAsync(string culture, string slug, int exceptPostId, CancellationToken ct)
    {
        var owner = await db.PostSlugs.Where(s => s.Culture == culture && s.Slug == slug && s.PostId != exceptPostId).Select(s => (int?)s.PostId)
            .FirstOrDefaultAsync(ct);
        return owner;
    }

    public async Task<IReadOnlyList<FeedEntry>> FeedAsync(long after, DateTimeOffset notAfter, int take, int? postId, CancellationToken ct)
    {
        var events = db.Events.Where(e => e.Id > after && e.At <= notAfter);
        if (postId is { } id)
        {
            var contents = db.PostContents.Where(c => c.PostId == id).Select(c => (Guid?)c.ContentId);
            events = events.Where(e => e.PostId == id || contents.Contains(e.ContentId));
        }
        return await events.OrderBy(e => e.Id).Take(take).AsNoTracking().ToListAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
