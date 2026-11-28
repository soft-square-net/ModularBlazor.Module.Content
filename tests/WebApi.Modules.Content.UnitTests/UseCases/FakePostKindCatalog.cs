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

    /// <summary>The values each kind defines; a kind not listed has a headline and a summary.</summary>
    public Dictionary<string, string[]> Values { get; } = [];

    public Task<IReadOnlyList<string>> ValueKeysAsync(string key, int version, CancellationToken ct) =>
        Down
            ? throw new PostKindCatalogUnavailableException("DynamicFields answered 503 Service Unavailable")
            : Task.FromResult<IReadOnlyList<string>>(Values.TryGetValue(key, out var keys) ? keys : ["headline", "description"]);

    public Task<IReadOnlyList<PostKindInfo>> ListAsync(CancellationToken ct)
    {
        Calls++;
        return Down
            ? throw new PostKindCatalogUnavailableException("DynamicFields answered 503 Service Unavailable")
            : Task.FromResult<IReadOnlyList<PostKindInfo>>([.. Kinds]);
    }
}
