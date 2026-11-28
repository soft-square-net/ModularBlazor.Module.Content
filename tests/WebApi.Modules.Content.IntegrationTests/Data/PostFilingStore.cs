using Microsoft.EntityFrameworkCore;
using soft.square.net.WebApi.Contracts.Security;
using soft.square.net.WebApi.Data;
using soft.square.net.WebApi.Modules.Content.Core.CategoryAggregate;
using soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate.Specifications;
using soft.square.net.WebApi.Modules.Content.Core.PressAggregate;
using soft.square.net.WebApi.Modules.Content.Infrastructure.Data;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts;

namespace soft.square.net.WebApi.Modules.Content.IntegrationTests.Data;

/// <summary>
/// A post's categories and unit, a content's unit, on a real database: the mapping (categories always loaded), the store's
/// unit and section queries, the live scopes, and the list filters (categories and unit grants) as SQL.
/// </summary>
public sealed class PostFilingStore : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Root = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid Desk = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly string RootPath = $"/{Root:N}";
    private static readonly string DeskPath = $"/{Root:N}/{Desk:N}";
    private readonly ContentDatabase _database = new();
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _database.Dispose();

    private async Task<(Post Filed, Post Plain)> SeedAsync()
    {
        await using var context = _database.For("acme");
        var politics = Category.Create("politics", "daily", "Politics", "السياسة");
        var economy = Category.Create("economy", "daily", "Economy");
        context.AddRange(Press.Create("daily", "Daily"), politics, economy);
        var filed = Post.Create("owner-1", politics, "Filed", "A body", Now, otherCategories: [economy]);
        filed.ChangeUnit("workflow", Desk, DeskPath, Now);
        var plain = Post.Create("owner-2", politics, "Plain", "A body", Now);
        context.AddRange(filed, plain);
        var content = ContentItem.Create("owner-1", "daily", "politics", null,
            new PostNode("", PostKinds.Article, "block.paragraph", 1, System.Text.Json.JsonDocument.Parse("""{"text":"x"}""").RootElement, System.Text.Json.JsonDocument.Parse("{}").RootElement, []),
            "assigned", Now, sectionId: "3");
        context.Add(content);
        await context.SaveChangesAsync(Ct);
        return (filed, plain);
    }

    [Fact]
    public async Task APostReadsBackWithItsCategoriesAndUnitWithoutAnInclude()
    {
        var (filed, _) = await SeedAsync();

        await using var context = _database.For("acme");
        var post = await new EfRepository<Post>(context).FirstOrDefaultAsync(new PostByIdReadSpec(filed.Id), Ct);

        post.ShouldNotBeNull();
        post.Categories.ShouldBe(["politics", "economy"]);
        post.UnitPath.ShouldBe(DeskPath);
        post.Scopes.ShouldContain($"unit:{Root:N}");
        (await context.Categories.SingleAsync(c => c.Id == "politics", Ct)).NameAr.ShouldBe("السياسة");
    }

    [Fact]
    public async Task ChangingTheCategoriesRewritesTheRows()
    {
        var (filed, _) = await SeedAsync();
        await using (var context = _database.For("acme"))
        {
            var post = await new EfRepository<Post>(context).FirstOrDefaultAsync(new PostByIdSpec(filed.Id), Ct);
            var economy = await context.Categories.SingleAsync(c => c.Id == "economy", Ct);
            post!.ChangeCategories("owner-1", economy, [], Now).ShouldBeTrue();
            await context.SaveChangesAsync(Ct);
        }

        await using var read = _database.For("acme");
        (await read.PostCategories.Where(c => c.PostId == filed.Id).Select(c => c.CategoryId).ToListAsync(Ct)).ShouldBe(["economy"]);
        (await read.Posts.SingleAsync(p => p.Id == filed.Id, Ct)).PrimaryCategoryId.ShouldBe("economy");
    }

    [Fact]
    public async Task TheStoreFindsTheUnitTreeAndTheSectionAndTheLiveScopes()
    {
        var (filed, plain) = await SeedAsync();

        await using var context = _database.For("acme");
        var store = new ContentStore(context);
        (await store.PostsInUnitTreeAsync(RootPath, Ct)).Select(p => p.Id).ShouldBe([filed.Id]);
        (await store.PostsInUnitTreeAsync(DeskPath, Ct)).Select(p => p.Id).ShouldBe([filed.Id]);
        (await store.PostsInUnitTreeAsync($"/{Guid.NewGuid():N}", Ct)).ShouldBeEmpty();
        (await store.ContentsInSectionAsync("3", Ct)).ShouldHaveSingleItem();
        (await store.ContentsInUnitTreeAsync(RootPath, Ct)).ShouldBeEmpty();
        (await store.PostScopesAsync(filed.Id, Ct))!.ShouldBe(["press:daily", "category:politics", "category:economy", $"unit:{Root:N}", $"unit:{Desk:N}"]);
        (await store.PostScopesAsync(plain.Id, Ct))!.ShouldBe(["press:daily", "category:politics"]);
    }

    [Theory]
    [InlineData("category:economy", "Filed")]
    [InlineData("unit:aaaaaaaa000000000000000000000001", "Filed")]
    [InlineData("category:politics", "Filed,Plain")]
    public async Task TheListFilterOfAGrantRunsAsSql(string scope, string expected)
    {
        await SeedAsync();
        var access = Substitute.For<IAccessScopes>();
        access.ScopesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([scope]);
        var posts = new PostAccess(access, TimeProvider.System);

        var filter = await posts.ScopeFilterAsync(PostAccess.SeeingPermissions, Ct);

        await using var context = _database.For("acme");
        (await context.Posts.Where(filter!).OrderBy(p => p.Title).Select(p => p.Title).ToListAsync(Ct)).ShouldBe(expected.Split(','));
    }
}
