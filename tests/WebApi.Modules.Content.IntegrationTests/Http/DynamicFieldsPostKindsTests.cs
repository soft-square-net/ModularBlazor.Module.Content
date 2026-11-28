using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using soft.square.net.WebApi.Contracts.Tenancy;
using soft.square.net.WebApi.Modules.Content.Infrastructure.Http;
using soft.square.net.WebApi.Modules.Content.UseCases;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Kinds;

namespace soft.square.net.WebApi.Modules.Content.IntegrationTests.Http;

/// <summary>The post kinds read from DynamicFields' root post fields over HTTP, with the caller's token and tenant, cached per tenant.</summary>
public sealed class DynamicFieldsPostKindsTests
{
    private const string Body = """
        [ { "key": "post.news", "title": "News", "description": "A news story.", "schemaType": "NewsArticle", "urlPrefix": "news", "version": 2 },
          { "key": "post.blog", "title": "Blog", "description": null, "schemaType": "BlogPosting", "urlPrefix": "ar/blog", "version": 1 } ]
        """;

    private readonly Handler _handler = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 1024 });
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private DynamicFieldsPostKinds Catalog(string tenant = "acme", string? token = "Bearer abc", int cacheSeconds = 60)
    {
        var context = new DefaultHttpContext();
        if (token is not null) context.Request.Headers.Authorization = token;
        context.Request.Headers[TenancyNames.Header] = tenant;
        return new DynamicFieldsPostKinds(new HttpClient(_handler), new HttpContextAccessor { HttpContext = context }, Substitute.For<ITenantContext>(), _cache,
            Options.Create(new ContentOptions { ApiBaseUrl = "http://gateway:5080/", PostKindCacheSeconds = cacheSeconds }));
    }

    [Fact]
    public async Task ReadsTheRootPostFieldsWithTheCallersTokenAndTenant()
    {
        _handler.Respond(HttpStatusCode.OK, Body);

        var kinds = await Catalog().ListAsync(Ct);

        kinds.ShouldBe([new PostKindInfo("post.news", "News", "news", "NewsArticle", 2), new PostKindInfo("post.blog", "Blog", "ar/blog", "BlogPosting", 1)]);
        var request = _handler.Requests.ShouldHaveSingleItem();
        request.Uri.ShouldBe("http://gateway:5080/api/dynamicfields/catalog/root-post-fields");
        request.Authorization.ShouldBe("Bearer abc");
        request.Tenant.ShouldBe("acme");
    }

    [Fact]
    public async Task CachesTheListPerTenant()
    {
        _handler.Respond(HttpStatusCode.OK, Body);

        await Catalog().ListAsync(Ct);
        await Catalog().ListAsync(Ct);
        await Catalog("globex").ListAsync(Ct);

        _handler.Requests.Select(r => r.Tenant).ShouldBe(["acme", "globex"]);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task AFailedAnswerIsUnavailableAndNotCached(HttpStatusCode status)
    {
        _handler.Respond(status, "{}");

        var e = await Should.ThrowAsync<PostKindCatalogUnavailableException>(() => Catalog().ListAsync(Ct));
        e.Message.ShouldContain(((int)status).ToString());

        _handler.Respond(HttpStatusCode.OK, Body);
        (await Catalog().ListAsync(Ct)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task AnUnreachableGatewayIsUnavailable()
    {
        _handler.Fail = true;

        await Should.ThrowAsync<PostKindCatalogUnavailableException>(() => Catalog().ListAsync(Ct));
    }

    [Fact]
    public async Task ReadsTheValueKeysOfAFieldVersionAndCachesThem()
    {
        _handler.Respond(HttpStatusCode.OK, """{ "field": { "key": "post.news", "version": 2, "properties": [ { "key": "headline" }, { "key": "dateline" } ] } }""");

        var first = await Catalog().ValueKeysAsync("post.news", 2, Ct);
        await Catalog().ValueKeysAsync("post.news", 2, Ct);

        first.ShouldBe(["headline", "dateline"]);
        var request = _handler.Requests.ShouldHaveSingleItem();
        request.Uri.ShouldBe("http://gateway:5080/api/dynamicfields/fields/post.news/versions/2");
        request.Authorization.ShouldBe("Bearer abc");
        request.Tenant.ShouldBe("acme");
    }

    [Fact]
    public async Task AFieldVersionWithoutValuesIsUnavailable()
    {
        _handler.Respond(HttpStatusCode.OK, "{}");

        await Should.ThrowAsync<PostKindCatalogUnavailableException>(() => Catalog().ValueKeysAsync("post.news", 2, Ct));
    }

    private sealed class Handler : HttpMessageHandler
    {
        private HttpStatusCode _status = HttpStatusCode.OK;
        private string _body = "[]";

        public bool Fail { get; set; }
        public List<(string Uri, string? Authorization, string? Tenant)> Requests { get; } = [];

        public void Respond(HttpStatusCode status, string body) => (_status, _body) = (status, body);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Fail) throw new HttpRequestException("Connection refused");
            Requests.Add((request.RequestUri!.ToString(), request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues(TenancyNames.Header, out var tenant) ? tenant.Single() : null));
            return Task.FromResult(new HttpResponseMessage(_status) { Content = new StringContent(_body, Encoding.UTF8, "application/json") });
        }
    }
}
