using Microsoft.EntityFrameworkCore;
using soft.square.net.WebApi.Data;
using soft.square.net.WebApi.Modules.Content.Core.CategoryAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate.Specifications;
using soft.square.net.WebApi.Modules.Content.Core.PressAggregate;

namespace soft.square.net.WebApi.Modules.Content.IntegrationTests.Data;

/// <summary>The module's mappings and specifications through the platform's repository, on a real database.</summary>
public sealed class EfRepositoryPosts : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);
    private readonly ContentDatabase _database = new();
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _database.Dispose();

    private async Task<Post> SeedPostAsync(string tenant = "acme")
    {
        await using var context = _database.For(tenant);
        var category = Category.Create("politics", "daily", "Politics");
        await new EfRepository<Press>(context).AddAsync(Press.Create("daily", "Daily"), Ct);
        await new EfRepository<Category>(context).AddAsync(category, Ct);
        var post = Post.Create("owner-1", category, "A title", "A body", Now);
        post.Submit("owner-1", Now);
        return await new EfRepository<Post>(context).AddAsync(post, Ct);
    }

    [Fact]
    public async Task StoresAPostWithItsHistoryAndTenant()
    {
        var saved = await SeedPostAsync();

        await using var context = _database.For("acme");
        var post = await new EfRepository<Post>(context).FirstOrDefaultAsync(new PostDetailSpec(saved.Id), Ct);

        post.ShouldNotBeNull();
        post.TenantId.ShouldBe("acme");
        post.State.ShouldBe(PostState.Submitted);
        post.History.Select(e => e.Action).ShouldBe(["created", "submitted"], ignoreOrder: true);
    }

    [Fact]
    public async Task AnotherTenantDoesNotSeeIt()
    {
        var saved = await SeedPostAsync();

        await using var other = _database.For("globex");
        (await new EfRepository<Post>(other).FirstOrDefaultAsync(new PostByIdSpec(saved.Id), Ct)).ShouldBeNull();

        await using var none = _database.For(null);
        (await new EfRepository<Post>(none).CountAsync(Ct)).ShouldBe(0);
    }

    [Fact]
    public async Task TheVersionIsAConcurrencyToken()
    {
        var saved = await SeedPostAsync();

        await using var first = _database.For("acme");
        await using var second = _database.For("acme");
        var mine = await new EfRepository<Post>(first).FirstOrDefaultAsync(new PostByIdSpec(saved.Id), Ct);
        var theirs = await new EfRepository<Post>(second).FirstOrDefaultAsync(new PostByIdSpec(saved.Id), Ct);

        mine!.Accept("manager-1", "shift-1", Now);
        await new EfRepository<Post>(first).UpdateAsync(mine, Ct);

        theirs!.Assign("manager-2", "reviewer-1", Now);
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => new EfRepository<Post>(second).UpdateAsync(theirs, Ct));
    }

    [Fact]
    public async Task ThePermalinkFindsOnlyPublishedPosts()
    {
        var saved = await SeedPostAsync();
        await using var context = _database.For("acme");
        var posts = new EfRepository<Post>(context);

        (await posts.FirstOrDefaultAsync(new PublishedPostByPublicIdSpec(saved.PublicId), Ct)).ShouldBeNull();

        var post = await posts.FirstOrDefaultAsync(new PostByIdSpec(saved.Id), Ct);
        post!.Accept("manager-1", "shift-1", Now);
        post.Assign("manager-1", "reviewer-1", Now);
        post.Approve("reviewer-1", Now);
        post.Publish("desk-1", Now);
        await posts.UpdateAsync(post, Ct);

        var published = await posts.FirstOrDefaultAsync(new PublishedPostByPublicIdSpec(saved.PublicId), Ct);
        published.ShouldNotBeNull();
        published.PublishedTitle.ShouldBe("A title");
    }
}
