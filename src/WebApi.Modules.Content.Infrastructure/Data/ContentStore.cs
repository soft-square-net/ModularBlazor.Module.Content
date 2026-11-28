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

    public async Task<IReadOnlyList<(Guid ContentId, int PostId)>> PostIdsUsingAsync(IReadOnlyCollection<Guid> contentIds, CancellationToken ct)
    {
        if (contentIds.Count == 0) return [];
        var rows = await db.PostContents.Where(c => contentIds.Contains(c.ContentId)).Select(c => new { c.ContentId, c.PostId })
            .Distinct().AsNoTracking().ToListAsync(ct);
        return rows.Select(r => (r.ContentId, r.PostId)).ToList();
    }

    public async Task<IReadOnlyCollection<string>?> PostScopesAsync(int postId, CancellationToken ct)
    {
        var post = await db.Posts.Where(p => p.Id == postId)
            .Select(p => new { p.PressId, p.PrimaryCategoryId, p.UnitPath, Categories = p.CategoryLinks.Select(c => c.CategoryId).ToList() })
            .AsNoTracking().FirstOrDefaultAsync(ct);
        return post is null ? null : Post.ScopesOf(post.PressId, [post.PrimaryCategoryId, .. post.Categories], post.UnitPath);
    }

    public async Task<IReadOnlyList<ContentItem>> ContentsInSectionAsync(string sectionId, CancellationToken ct) =>
        await db.Contents.Where(c => c.SectionId == sectionId && c.UnitId == null).ToListAsync(ct);

    public async Task<IReadOnlyList<Post>> PostsInUnitTreeAsync(string path, CancellationToken ct)
    {
        var below = path + "/";
        return await db.Posts.Where(p => p.UnitPath == path || p.UnitPath!.StartsWith(below)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ContentItem>> ContentsInUnitTreeAsync(string path, CancellationToken ct)
    {
        var below = path + "/";
        return await db.Contents.Where(c => c.UnitPath == path || c.UnitPath!.StartsWith(below)).ToListAsync(ct);
    }

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

    public Task<Post?> TrackedPostAsync(int postId, CancellationToken ct) => db.Posts.FirstOrDefaultAsync(p => p.Id == postId, ct);

    public Task<bool> IsWorkflowContentAsync(Guid contentId, string? originPostId, CancellationToken ct) =>
        db.Posts.AnyAsync(p => p.WorkflowProjectId != null && !p.WorkflowProjectEnded
            && (p.ContentLinks.Any(c => c.ContentId == contentId) || (originPostId != null && p.PublicId == originPostId)), ct);

    public async Task<(int PostId, string OwnerId)?> CreateKeyAsync(string keyHash, CancellationToken ct)
    {
        var found = await db.PostCreateKeys.AsNoTracking().Where(k => k.KeyHash == keyHash)
            .Select(k => new { k.PostId, k.OwnerId }).FirstOrDefaultAsync(ct);
        return found is null ? null : (found.PostId, found.OwnerId);
    }

    public void Add(PostCreateKey key) => db.PostCreateKeys.Add(key);

    public async Task<bool> TryCreateOnceAsync(string keyHash, Func<Task> work, CancellationToken ct)
    {
        try
        {
            await InTransactionAsync(work, ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Rolled back. If the key is there now, another request won the race; otherwise the failure is a real one.
            db.ChangeTracker.Clear();
            if (await CreateKeyAsync(keyHash, ct) is null) throw;
            return false;
        }
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public async Task InTransactionAsync(Func<Task> work, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null)
        {
            await work();
            return;
        }
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await work();
            await transaction.CommitAsync(ct);
        });
    }
}
