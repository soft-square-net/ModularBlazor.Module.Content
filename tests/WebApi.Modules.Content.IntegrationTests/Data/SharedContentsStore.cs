using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using soft.square.net.WebApi.Data;
using soft.square.net.WebApi.Modules.Content.Core.CategoryAggregate;
using soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;
using soft.square.net.WebApi.Modules.Content.Core.ContentAggregate.Specifications;
using soft.square.net.WebApi.Modules.Content.Core.FeedAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PipelineAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate.Specifications;
using soft.square.net.WebApi.Modules.Content.Core.PressAggregate;
using soft.square.net.WebApi.Modules.Content.Infrastructure.Data;

namespace soft.square.net.WebApi.Modules.Content.IntegrationTests.Data;

/// <summary>Shared contents, post references, the feed and the migration's backfill, on a real database.</summary>
public sealed class SharedContentsStore : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly ContentDatabase _database = new();
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _database.Dispose();

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static PostNode Paragraph(string id, string text) =>
        new(id, PostKinds.Article, "block.paragraph", 1, Json($$"""{ "text": "{{text}}" }"""), Json("{}"), []);

    private static PostDocument Document(params PostNode[] children) =>
        new("post.news", 1, Json("""{ "headline": "Final day" }"""), Json("{}"), children);

    private async Task<Category> SeedCategoryAsync()
    {
        await using var context = _database.For("acme");
        var category = Category.Create("politics", "daily", "Politics");
        await new EfRepository<Press>(context).AddAsync(Press.Create("daily", "Daily"), Ct);
        await new EfRepository<Category>(context).AddAsync(category, Ct);
        return category;
    }

    /// <summary>A post with two contents (p1, p2), saved in one save.</summary>
    private async Task<(int PostId, Guid First, Guid Second)> SeedPostAsync(Category category, string title = "A title")
    {
        await using var context = _database.For("acme");
        var publicId = Post.NewPublicId();
        var a = ContentItem.Create("owner-1", "daily", "politics", publicId, Paragraph("p1", "One"), "assigned", Now);
        var b = ContentItem.Create("owner-1", "daily", "politics", publicId, Paragraph("p2", "Two"), "assigned", Now);
        var items = new Dictionary<Guid, ContentItem> { [a.Id] = a, [b.Id] = b };
        var root = Document();
        var post = Post.Create("owner-1", category, title, "", Now, PostKinds.News, PostComposition.Working(root, [a.Id, b.Id], items),
            [new ContentRef(a.Id), new ContentRef(b.Id)], publicId);
        context.Contents.AddRange(a, b);
        context.Posts.Add(post);
        await context.SaveChangesAsync(Ct);
        return (post.Id, a.Id, b.Id);
    }

    [Fact]
    public async Task StoresAPostWithItsContentsInOrder()
    {
        var category = await SeedCategoryAsync();
        var (postId, first, second) = await SeedPostAsync(category);

        await using var context = _database.For("acme");
        var post = await new EfRepository<Post>(context).FirstOrDefaultAsync(new PostDetailSpec(postId), Ct);
        var content = await new EfRepository<ContentItem>(context).FirstOrDefaultAsync(new ContentDetailSpec(first), Ct);

        post!.Contents.Select(c => c.ContentId).ShouldBe([first, second]);
        post.ContentsSplit.ShouldBeTrue();
        content!.TenantId.ShouldBe("acme");
        content.Revisions.ShouldHaveSingleItem().Node.Content.GetProperty("text").GetString().ShouldBe("One");
        content.History.ShouldHaveSingleItem().Action.ShouldBe("created");
        (await context.Events.CountAsync(e => e.Type == FeedEventTypes.ContentCreated, Ct)).ShouldBe(2);
    }

    [Fact]
    public async Task EditingAContentRecomposesThePostsThatUseItAndFeedsThem()
    {
        var category = await SeedCategoryAsync();
        var (postId, first, _) = await SeedPostAsync(category);
        var (otherId, _, _) = await SeedPostAsync(category, "Other");

        await using (var context = _database.For("acme"))
        {
            var store = new ContentStore(context);
            var content = (await store.ContentsAsync([first], Ct)).Single();
            content.Edit("owner-1", Paragraph("p1", "One, edited"), PipelineDefinition.Default.Stage("assigned"), Now);
            await store.SaveChangesAsync(Ct);
        }

        await using var reader = _database.For("acme");
        var post = await reader.Posts.AsNoTracking().SingleAsync(p => p.Id == postId, Ct);
        JsonSerializer.Deserialize<PostDocument>(post.DocumentJson, Web)!.Children[0].Content.GetProperty("text").GetString().ShouldBe("One, edited");

        var feed = await new ContentStore(reader).FeedAsync(0, Now.AddDays(1), 100, postId, Ct);
        // The first edit also starts the work: assigned → in-progress.
        feed.Select(e => e.Type).ShouldBe([FeedEventTypes.ContentCreated, FeedEventTypes.ContentCreated, FeedEventTypes.ContentChanged,
            FeedEventTypes.ContentStateChanged]);
        feed[2].Revision.ShouldBe(2);
        feed[3].Stage.ShouldBe("in-progress");
        var other = await new ContentStore(reader).FeedAsync(0, Now.AddDays(1), 100, otherId, Ct);
        other.ShouldNotContain(e => e.ContentId == first);
        (await new ContentStore(reader).FeedAsync(feed[0].Id, Now.AddDays(1), 100, null, Ct)).Count.ShouldBe(5); // all but the first
    }

    [Fact]
    public async Task KnowsWhetherAContentIsUsedAndWhoOwnsASlug()
    {
        var category = await SeedCategoryAsync();
        var (postId, first, _) = await SeedPostAsync(category);

        await using (var context = _database.For("acme"))
        {
            var post = await context.Posts.Include(p => p.ContentLinks).Include(p => p.Slugs).SingleAsync(p => p.Id == postId, Ct);
            post.Edit(PostEditor.Owner, "owner-1", Now, TimeSpan.FromHours(1), new PostChange(Slugs: new Dictionary<string, string> { ["en"] = "final-day" }));
            await context.SaveChangesAsync(Ct);
        }

        await using var reader = _database.For("acme");
        var store = new ContentStore(reader);
        (await store.IsUsedAsync(first, Ct)).ShouldBeTrue();
        (await store.IsUsedAsync(Guid.NewGuid(), Ct)).ShouldBeFalse();
        (await store.SlugOwnerAsync("en", "final-day", 0, Ct)).ShouldBe(postId);
        (await store.SlugOwnerAsync("en", "final-day", postId, Ct)).ShouldBeNull();
        (await reader.Events.AnyAsync(e => e.PostId == postId && e.Type == FeedEventTypes.PostChanged, Ct)).ShouldBeTrue();
    }

    [Fact]
    public async Task TheBackfillSplitsStoredPostsOnce()
    {
        var category = await SeedCategoryAsync();
        int postId;
        await using (var context = _database.For("acme"))
        {
            var post = Post.Create("owner-1", category, "Legacy", "", Now, document: Document(Paragraph("p1", "One"), Paragraph("p2", "Two")));
            post.MarkUnsplit();
            context.Posts.Add(post);
            await context.SaveChangesAsync(Ct);
            postId = post.Id;
        }

        await using (var context = _database.For("acme"))
        {
            (await ContentBackfill.SplitAsync(context, Now, Ct)).ShouldBe(1);
            (await ContentBackfill.SplitAsync(context, Now, Ct)).ShouldBe(0);
        }

        await using var reader = _database.For("acme");
        var split = await new EfRepository<Post>(reader).FirstOrDefaultAsync(new PostDetailSpec(postId), Ct);
        var contents = await reader.Contents.AsNoTracking().ToListAsync(Ct);
        split!.ContentsSplit.ShouldBeTrue();
        contents.Select(c => c.NodeId).ShouldBe(["p1", "p2"], ignoreOrder: true);
        split.Contents.Select(c => c.ContentId).ShouldBe([contents.Single(c => c.NodeId == "p1").Id, contents.Single(c => c.NodeId == "p2").Id]);
        (await reader.Events.CountAsync(Ct)).ShouldBe(0); // the migration is not news
    }
}
