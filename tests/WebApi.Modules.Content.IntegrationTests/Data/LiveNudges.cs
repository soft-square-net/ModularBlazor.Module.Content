using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using soft.square.net.WebApi.Contracts.Live;
using soft.square.net.WebApi.Data;
using soft.square.net.WebApi.Modules.Content.Core.CategoryAggregate;
using soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;
using soft.square.net.WebApi.Modules.Content.Core.FeedAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PipelineAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PressAggregate;
using soft.square.net.WebApi.Modules.Content.Infrastructure.Data;
using soft.square.net.WebApi.Modules.Content.UseCases.Live;

namespace soft.square.net.WebApi.Modules.Content.IntegrationTests.Data;

/// <summary>
/// The live nudges on <c>content:post:{id}</c>, through the real DbContext: sent once the feed rows are committed (after
/// the save, or when its transaction commits), never for a rollback, and never failing the save.
/// </summary>
public sealed class LiveNudges : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);
    private readonly ContentDatabase _database = new();
    private readonly Notifier _live = new();
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _database.Dispose();

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static PostNode Paragraph(string id, string text) =>
        new(id, PostKind.Article, "block.paragraph", 1, Json($$"""{ "text": "{{text}}" }"""), Json("{}"), []);

    private async Task<Category> SeedCategoryAsync()
    {
        await using var context = _database.For("acme");
        var category = Category.Create("politics", "daily", "Politics");
        await new EfRepository<Press>(context).AddAsync(Press.Create("daily", "Daily"), Ct);
        await new EfRepository<Category>(context).AddAsync(category, Ct);
        return category;
    }

    /// <summary>A post that uses one content: <paramref name="shared"/>, or a new one.</summary>
    private async Task<(int PostId, Guid ContentId)> SeedPostAsync(Category category, Guid? shared = null)
    {
        await using var context = _database.For("acme");
        var publicId = Post.NewPublicId();
        ContentItem content;
        if (shared is { } id) content = await context.Contents.SingleAsync(c => c.Id == id, Ct);
        else
        {
            content = ContentItem.Create("owner-1", "daily", "politics", publicId, Paragraph("p1", "One"), "draft", Now);
            context.Contents.Add(content);
        }
        var root = new PostDocument("post.news", 1, Json("""{ "headline": "Final day" }"""), Json("{}"), []);
        var post = Post.Create("owner-1", category, "A title", "", Now, PostKind.News,
            PostComposition.Working(root, [content.Id], new Dictionary<Guid, ContentItem> { [content.Id] = content }),
            [new ContentRef(content.Id)], publicId);
        context.Posts.Add(post);
        await context.SaveChangesAsync(Ct);
        return (post.Id, content.Id);
    }

    private async Task<Post> LoadAsync(ContentDbContext context, int postId) =>
        await context.Posts.Include(p => p.ContentLinks).Include(p => p.Slugs).SingleAsync(p => p.Id == postId, Ct);

    private static void EditTitle(Post post, string title) =>
        post.Edit(PostEditor.Owner, "owner-1", Now, TimeSpan.FromDays(1), new PostChange(Title: title));

    [Fact]
    public async Task APostEditNudgesThePostAfterTheSave()
    {
        var (postId, _) = await SeedPostAsync(await SeedCategoryAsync());

        await using var context = _database.For("acme", _live);
        EditTitle(await LoadAsync(context, postId), "Edited");
        await context.SaveChangesAsync(Ct);

        var row = await context.Events.Where(e => e.PostId == postId).OrderByDescending(e => e.Id).FirstAsync(Ct);
        var sent = _live.Messages.ShouldHaveSingleItem();
        sent.Topic.ShouldBe($"content:post:{postId}");
        sent.Type.ShouldBe(PostLive.MessageType);
        sent.Sequence.ShouldBeNull();
        sent.Payload!.Value.GetProperty("eventId").GetInt64().ShouldBe(row.Id);
        sent.Payload.Value.GetProperty("type").GetString().ShouldBe(FeedEventTypes.PostChanged);
    }

    [Fact]
    public async Task AContentEditNudgesEveryPostThatUsesIt()
    {
        var category = await SeedCategoryAsync();
        var (first, content) = await SeedPostAsync(category);
        var (second, _) = await SeedPostAsync(category, content);
        var (unrelated, _) = await SeedPostAsync(category);

        await using (var context = _database.For("acme", _live))
        {
            var store = new ContentStore(context);
            var item = (await store.ContentsAsync([content], Ct)).Single();
            item.Edit("owner-1", Paragraph("p1", "One, edited"), PipelineDefinition.Default.Stage("draft"), Now);
            await store.SaveChangesAsync(Ct);
        }

        var changed = _live.Messages.Where(m => m.Payload!.Value.GetProperty("type").GetString() == FeedEventTypes.ContentChanged).ToList();
        changed.Select(m => m.Topic).Order().ShouldBe(new[] { $"content:post:{first}", $"content:post:{second}" }.Order());
        changed.ShouldAllBe(m => m.Payload!.Value.GetProperty("contentId").GetGuid() == content);
        _live.Messages.ShouldNotContain(m => m.Topic == $"content:post:{unrelated}");
    }

    [Fact]
    public async Task AFailedPublishDoesNotFailTheSave()
    {
        var (postId, _) = await SeedPostAsync(await SeedCategoryAsync());
        _live.Fail = true;

        await using (var context = _database.For("acme", _live))
        {
            EditTitle(await LoadAsync(context, postId), "Saved anyway");
            await Should.NotThrowAsync(() => context.SaveChangesAsync(Ct));
        }

        await using var reader = _database.For("acme");
        (await reader.Posts.SingleAsync(p => p.Id == postId, Ct)).Title.ShouldBe("Saved anyway");
    }

    [Fact]
    public async Task InATransactionTheNudgeWaitsForTheCommit()
    {
        var (postId, _) = await SeedPostAsync(await SeedCategoryAsync());

        await using var context = _database.For("acme", _live);
        await using var transaction = await context.Database.BeginTransactionAsync(Ct);
        EditTitle(await LoadAsync(context, postId), "In a transaction");
        await context.SaveChangesAsync(Ct);
        _live.Messages.ShouldBeEmpty();

        await transaction.CommitAsync(Ct);
        _live.Messages.ShouldHaveSingleItem().Topic.ShouldBe($"content:post:{postId}");
    }

    [Fact]
    public async Task ARolledBackSaveSendsNothing()
    {
        var (postId, _) = await SeedPostAsync(await SeedCategoryAsync());

        await using (var context = _database.For("acme", _live))
        {
            await using var transaction = await context.Database.BeginTransactionAsync(Ct);
            EditTitle(await LoadAsync(context, postId), "Rolled back");
            await context.SaveChangesAsync(Ct);
            await transaction.RollbackAsync(Ct);
        }

        _live.Messages.ShouldBeEmpty();
        await using var reader = _database.For("acme");
        (await reader.Posts.SingleAsync(p => p.Id == postId, Ct)).Title.ShouldBe("A title");
    }

    [Fact]
    public async Task TheResolverReadsThePostsScopesInItsTenant()
    {
        var (postId, _) = await SeedPostAsync(await SeedCategoryAsync());

        await using var acme = _database.For("acme");
        (await new PostLiveScopeResolver(new ContentStore(acme)).ScopesAsync("post", postId.ToString(), Ct))
            .ShouldBe(["press:daily", "category:politics"]);
        await using var other = _database.For("globex");
        (await new PostLiveScopeResolver(new ContentStore(other)).ScopesAsync("post", postId.ToString(), Ct)).ShouldBeEmpty();
    }

    private sealed class Notifier : ILiveNotifier
    {
        public sealed record Sent(string Topic, string Type, JsonElement? Payload, long? Sequence);

        public List<Sent> Messages { get; } = [];
        public bool Fail { get; set; }

        public Task PublishAsync(string topic, string type, JsonElement? payload = null, long? sequence = null, CancellationToken ct = default)
        {
            if (Fail) throw new InvalidOperationException("The bus is down.");
            Messages.Add(new Sent(topic, type, payload, sequence));
            return Task.CompletedTask;
        }

        public Task PublishToUnitAsync(string unitPath, string type, JsonElement? payload = null, long? sequence = null, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
