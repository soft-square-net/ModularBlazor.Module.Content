using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Kinds;

namespace soft.square.net.WebApi.Modules.Content.UnitTests.UseCases;

/// <summary>DynamicFields' published root post fields, in memory: article and news by default; <see cref="Down"/> fails every call.</summary>
public sealed class FakePostKindCatalog : IPostKindCatalog
{
    public List<PostKindInfo> Kinds { get; } =
    [
        new(PostKinds.Article, "Article", "articles", "Article"),
        new(PostKinds.News, "News", "news", "NewsArticle")
    ];

    public bool Down { get; set; }
    public int Calls { get; private set; }

    public Task<IReadOnlyList<PostKindInfo>> ListAsync(CancellationToken ct)
    {
        Calls++;
        return Down
            ? throw new PostKindCatalogUnavailableException("DynamicFields answered 503 Service Unavailable")
            : Task.FromResult<IReadOnlyList<PostKindInfo>>([.. Kinds]);
    }
}
