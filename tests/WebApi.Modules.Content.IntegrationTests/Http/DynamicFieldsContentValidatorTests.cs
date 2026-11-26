using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using soft.square.net.Content.Contracts;
using soft.square.net.WebApi.Contracts.Tenancy;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.Infrastructure.Http;
using soft.square.net.WebApi.Modules.Content.UseCases;
using soft.square.net.WebApi.Modules.Content.UseCases.Validation;

namespace soft.square.net.WebApi.Modules.Content.IntegrationTests.Http;

/// <summary>DynamicFields' validate-node and validate over HTTP, with the caller's token and tenant.</summary>
public sealed class DynamicFieldsContentValidatorTests
{
    private readonly Handler _handler = new();
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static readonly PostNode Image = new("i1", PostKinds.Article, "block.image", 2, Json("""{ "src": "" }"""), Json("{}"), []);

    private DynamicFieldsContentValidator Validator()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer abc";
        context.Request.Headers[TenancyNames.Header] = "acme";
        return new DynamicFieldsContentValidator(new HttpClient(_handler), new HttpContextAccessor { HttpContext = context },
            Substitute.For<ITenantContext>(), Options.Create(new ContentOptions { ApiBaseUrl = "http://gateway:5080/" }));
    }

    [Fact]
    public async Task ChecksANodeWithTheCallersTokenAndTenant()
    {
        _handler.Respond(HttpStatusCode.OK, """{ "valid": false, "issues": [ { "path": "/content/src", "message": "Required." } ] }""");

        var problems = await Validator().ValidateNodeAsync(Image, Ct);

        problems.ShouldBe([new ContentProblem("/content/src", "Required.", null)]);
        var request = _handler.Requests.ShouldHaveSingleItem();
        request.Uri.ShouldBe("http://gateway:5080/api/dynamicfields/content/validate-node");
        request.Authorization.ShouldBe("Bearer abc");
        request.Tenant.ShouldBe("acme");
        var body = JsonDocument.Parse(request.Body).RootElement;
        body.GetProperty("field").GetString().ShouldBe("block.image");
        body.GetProperty("fieldVersion").GetInt32().ShouldBe(2);
        body.GetProperty("content").GetProperty("src").GetString().ShouldBe("");
    }

    [Fact]
    public async Task AnUnpublishedFieldVersionIsAProblem()
    {
        _handler.Respond(HttpStatusCode.NotFound, """{ "status": 404 }""");

        var problem = (await Validator().ValidateNodeAsync(Image, Ct)).ShouldHaveSingleItem();

        problem.Path.ShouldBe("/field");
        problem.Message.ShouldContain("block.image");
    }

    [Fact]
    public async Task SendsTheWholeTree()
    {
        _handler.Respond(HttpStatusCode.OK, """{ "valid": true, "issues": [] }""");
        var document = new PostDocument("post.news", 1, Json("""{ "headline": "x" }"""), Json("{}"), [Image with { Children = [Image with { NodeId = "i2" }] }]);

        (await Validator().ValidateTreeAsync(document, Ct)).ShouldBeEmpty();

        var request = _handler.Requests.ShouldHaveSingleItem();
        request.Uri.ShouldEndWith("/api/dynamicfields/content/validate");
        var root = JsonDocument.Parse(request.Body).RootElement.GetProperty("root");
        root.GetProperty("field").GetString().ShouldBe("post.news");
        root.GetProperty("children")[0].GetProperty("nodeId").GetString().ShouldBe("i1");
        root.GetProperty("children")[0].GetProperty("children")[0].GetProperty("nodeId").GetString().ShouldBe("i2");
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task AFailedAnswerIsUnavailable(HttpStatusCode status)
    {
        _handler.Respond(status, "{}");

        var e = await Should.ThrowAsync<ContentValidatorUnavailableException>(() => Validator().ValidateNodeAsync(Image, Ct));
        e.Message.ShouldContain(((int)status).ToString());
        await Should.ThrowAsync<ContentValidatorUnavailableException>(() =>
            Validator().ValidateTreeAsync(new PostDocument("post.news", 1, Json("{}"), Json("{}"), []), Ct));
    }

    [Fact]
    public async Task AnUnreachableGatewayIsUnavailable()
    {
        _handler.Fail = true;

        await Should.ThrowAsync<ContentValidatorUnavailableException>(() => Validator().ValidateNodeAsync(Image, Ct));
    }

    private sealed class Handler : HttpMessageHandler
    {
        private HttpStatusCode _status = HttpStatusCode.OK;
        private string _body = "{}";

        public bool Fail { get; set; }
        public List<(string Uri, string? Authorization, string? Tenant, string Body)> Requests { get; } = [];

        public void Respond(HttpStatusCode status, string body) => (_status, _body) = (status, body);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Fail) throw new HttpRequestException("Connection refused");
            Requests.Add((request.RequestUri!.ToString(), request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues(TenancyNames.Header, out var tenant) ? tenant.Single() : null,
                request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(_status) { Content = new StringContent(_body, Encoding.UTF8, "application/json") };
        }
    }
}
