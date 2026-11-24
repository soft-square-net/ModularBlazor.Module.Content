using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using soft.square.net.WebApi.Contracts.Tenancy;
using soft.square.net.WebApi.Data;
using soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PipelineAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.Infrastructure.Data;

/// <summary>
/// The data step of the <c>SharedContents</c> migration, per tenant, after the schema migrations (registered after them, so
/// it runs after them): the posts not split yet get their contents (<see cref="PostSplit"/>), a batch per save. Idempotent:
/// it only reads rows still flagged unsplit, and one replica at a time does it (the tenant database's advisory lock).
/// Done in C#, not SQL, since splitting JSON documents in four SQL dialects would be fragile.
/// </summary>
public sealed class ContentBackfill(IServiceProvider services, TenantDatabases databases, TimeProvider clock, ILogger<ContentBackfill> log)
    : ITenantInitializer
{
    private const int Batch = 100;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task InitializeAsync(TenantInfo tenant, CancellationToken ct)
    {
        await using var scope = services.CreateTenantScope(tenant);
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
        if (!await db.Posts.AnyAsync(p => !p.ContentsSplit, ct)) return;

        await using (await databases.LockAsync(tenant, "content:split-posts", ct))
        {
            var count = await SplitAsync(db, clock.GetUtcNow(), ct);
            if (count > 0) log.LogInformation("Split {Count} posts into shared contents for tenant {Tenant}", count, tenant.Id);
        }
    }

    /// <summary>Splits every unsplit post of the context's tenant; returns how many.</summary>
    public static async Task<int> SplitAsync(ContentDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        var pipeline = (await db.Pipelines.OrderBy(p => p.Id).FirstOrDefaultAsync(ct))?.Definition ?? PipelineDefinition.Default;
        var total = 0;
        while (true)
        {
            var posts = await db.Posts.Where(p => !p.ContentsSplit).Include(p => p.ContentLinks).OrderBy(p => p.Id).Take(Batch).ToListAsync(ct);
            if (posts.Count == 0) return total;
            foreach (var post in posts)
            {
                var document = string.IsNullOrWhiteSpace(post.DocumentJson) || post.DocumentJson == "{}"
                    ? PostDocument.FromLegacy(post.Title, post.Body)
                    : JsonSerializer.Deserialize<PostDocument>(post.DocumentJson, Json) ?? PostDocument.FromLegacy(post.Title, post.Body);
                db.Contents.AddRange(PostSplit.Migrate(post, document, pipeline, now));
            }
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            total += posts.Count;
        }
    }
}
