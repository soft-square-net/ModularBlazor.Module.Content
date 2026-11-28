using soft.square.net.WebApi.Data;
using soft.square.net.WebApi.Modules.Content.Core.CategoryAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate.Specifications;
using soft.square.net.WebApi.Modules.Content.Core.PressAggregate;
using soft.square.net.WebApi.Modules.Content.Infrastructure.Data;

namespace soft.square.net.WebApi.Modules.Content.IntegrationTests.Data;

/// <summary>The cutover's queries and the create keys, on a real database.</summary>
public sealed class CutoverStore : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 28, 9, 0, 0, TimeSpan.Zero);
    private readonly ContentDatabase _database = new();
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _database.Dispose();

    private async Task<Category> SeedCategoryAsync()
    {
        await using var context = _database.For("acme");
        var category = Category.Create("politics", "daily", "Politics");
        context.AddRange(Press.Create("daily", "Daily"), category);
        await context.SaveChangesAsync(Ct);
        return category;
    }

    private async Task<Post> AddPostAsync(Category category, Action<Post>? change = null)
    {
        await using var context = _database.For("acme");
        var post = Post.Create("owner-1", category, "A title", "A body", Now);
        change?.Invoke(post);
        return await new EfRepository<Post>(context).AddAsync(post, Ct);
    }

    private async Task<bool> CreateWithKeyAsync(string key, string owner)
    {
        await using var context = _database.For("acme");
        var store = new ContentStore(context);
        var category = await context.Categories.FindAsync(["politics"], Ct);
        return await store.TryCreateOnceAsync(PostCreateKey.Hash(key), async () =>
        {
            var post = Post.Create(owner, category!, "A title", "A body", Now);
            context.Add(post);
            await context.SaveChangesAsync(Ct);
            store.Add(new PostCreateKey(key, owner, post.Id, Now));
            await context.SaveChangesAsync(Ct);
        }, Ct);
    }

    [Fact]
    public async Task ASecondWriteOfAKeyKeepsNothing()
    {
        await SeedCategoryAsync();

        (await CreateWithKeyAsync("newsroom:7", "owner-1")).ShouldBeTrue();
        (await CreateWithKeyAsync("newsroom:7", "owner-2")).ShouldBeFalse();
        (await CreateWithKeyAsync("Newsroom:7", "owner-2")).ShouldBeTrue(); // keys compare exactly

        await using var context = _database.For("acme");
        (await new EfRepository<Post>(context).CountAsync(Ct)).ShouldBe(2);
        (await new ContentStore(context).CreateKeyAsync(PostCreateKey.Hash("newsroom:7"), Ct))!.Value.OwnerId.ShouldBe("owner-1");
    }

    [Fact]
    public async Task AContentIsTheWorkflowsWhileAPostWithAProjectUsesIt()
    {
        var category = await SeedCategoryAsync();
        var used = Guid.NewGuid();
        var project = Guid.NewGuid();
        var managed = await AddPostAsync(category, p => p.FollowWorkflowProject(project, ended: false));
        var plain = await AddPostAsync(category);
        var shared = Guid.NewGuid();
        await AddPostAsync(category, p =>
        {
            p.FollowWorkflowProject(Guid.NewGuid(), ended: false);
            p.MarkUnsplit();
            p.CompleteSplit([shared], pinned: false); // uses it, wherever it was made
        });

        await using (var context = _database.For("acme"))
        {
            var store = new ContentStore(context);
            (await store.IsWorkflowContentAsync(used, managed.PublicId, Ct)).ShouldBeTrue(); // made for it
            (await store.IsWorkflowContentAsync(used, plain.PublicId, Ct)).ShouldBeFalse();
            (await store.IsWorkflowContentAsync(used, null, Ct)).ShouldBeFalse();
            (await store.IsWorkflowContentAsync(shared, plain.PublicId, Ct)).ShouldBeTrue();
            (await store.TrackedPostAsync(managed.Id, Ct))!.FollowWorkflowProject(project, ended: true);
            await store.SaveChangesAsync(Ct);
        }
        await using (var context = _database.For("acme"))
            (await new ContentStore(context).IsWorkflowContentAsync(used, managed.PublicId, Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task TheClockReadsThePostsInReviewInBatchesById()
    {
        var category = await SeedCategoryAsync();
        void InReview(Post p)
        {
            p.Submit("owner-1", Now);
            p.Assign("manager-1", "reviewer-1", Now);
        }
        var first = await AddPostAsync(category, InReview);
        await AddPostAsync(category);
        var second = await AddPostAsync(category, InReview);

        await using var context = _database.For("acme");
        var posts = new EfRepository<Post>(context);

        (await posts.ListAsync(new InReviewPostIdsSpec(0, 1), Ct)).ShouldBe([first.Id]);
        (await posts.ListAsync(new InReviewPostIdsSpec(first.Id, 10), Ct)).ShouldBe([second.Id]);
    }
}
