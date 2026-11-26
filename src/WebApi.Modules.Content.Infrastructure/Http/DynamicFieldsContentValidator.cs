using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using soft.square.net.Content.Contracts;
using soft.square.net.WebApi.Contracts.Tenancy;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UseCases;
using soft.square.net.WebApi.Modules.Content.UseCases.Validation;

namespace soft.square.net.WebApi.Modules.Content.Infrastructure.Http;

/// <summary>
/// <see cref="IContentValidator"/> over DynamicFields' API, through the gateway (<c>Content:ApiBaseUrl</c>):
/// <c>POST /api/dynamicfields/content/validate-node</c> (<c>DynamicFields_ValidateNode</c>) and
/// <c>POST /api/dynamicfields/content/validate</c> (<c>DynamicFields_ValidateContent</c>), both <c>dynamicfields.read</c>.
/// The call carries the caller's access token and tenant. Nothing is cached: the answer depends on the values.
/// </summary>
public sealed class DynamicFieldsContentValidator(HttpClient http, IHttpContextAccessor accessor, ITenantContext tenant,
    IOptions<ContentOptions> options) : IContentValidator
{
    public const string NodePath = "/api/dynamicfields/content/validate-node";
    public const string TreePath = "/api/dynamicfields/content/validate";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>DynamicFields' <c>ContentNode</c>.</summary>
    private sealed record NodeWire(string Field, int FieldVersion, JsonElement Content, JsonElement Style, IReadOnlyList<NodeWire>? Children,
        string? NodeId);

    /// <summary>DynamicFields' <c>ContentReport</c>.</summary>
    private sealed record ReportWire(bool Valid, List<IssueWire>? Issues);

    private sealed record IssueWire(string? Path, string? Message);

    public async Task<IReadOnlyList<ContentProblem>> ValidateNodeAsync(PostNode node, CancellationToken ct)
    {
        var body = new { field = node.SchemaId, fieldVersion = node.SchemaVersion, content = node.Content, style = node.Style };
        return await SendAsync(NodePath, body, notFound: $"Field '{node.SchemaId}' version {node.SchemaVersion} is not published.", ct);
    }

    public async Task<IReadOnlyList<ContentProblem>> ValidateTreeAsync(PostDocument document, CancellationToken ct)
    {
        var root = new NodeWire(document.SchemaId, document.SchemaVersion, document.Content, document.Style,
            [.. (document.Children ?? []).Select(Wire)], null);
        return await SendAsync(TreePath, new { root }, notFound: null, ct);
    }

    private static NodeWire Wire(PostNode node) =>
        new(node.SchemaId, node.SchemaVersion, node.Content, node.Style, [.. (node.Children ?? []).Select(Wire)], node.NodeId);

    private async Task<IReadOnlyList<ContentProblem>> SendAsync(string path, object body, string? notFound, CancellationToken ct)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.ApiBaseUrl)) throw new ContentValidatorUnavailableException("Content:ApiBaseUrl is not set");
        var incoming = accessor.HttpContext?.Request;
        var tenantId = incoming?.Headers[TenancyNames.Header].ToString() is { Length: > 0 } header ? header : tenant.TenantId;
        using var request = new HttpRequestMessage(HttpMethod.Post, o.ApiBaseUrl.TrimEnd('/') + path) { Content = JsonContent.Create(body, options: Json) };
        if (incoming?.Headers.Authorization.ToString() is { Length: > 0 } authorization)
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
        if (tenantId is not null) request.Headers.TryAddWithoutValidation(TenancyNames.Header, tenantId);

        try
        {
            using var response = await http.SendAsync(request, ct);
            // validate-node: 404 is DynamicFields' answer for a field version it does not publish, a problem of the content.
            if (response.StatusCode == HttpStatusCode.NotFound && notFound is not null) return [new ContentProblem("/field", notFound, null)];
            if (!response.IsSuccessStatusCode)
                throw new ContentValidatorUnavailableException($"DynamicFields answered {(int)response.StatusCode} {response.ReasonPhrase}");
            var report = await response.Content.ReadFromJsonAsync<ReportWire>(Json, ct)
                         ?? throw new ContentValidatorUnavailableException("DynamicFields sent no report");
            var issues = report.Issues ?? [];
            if (!report.Valid && issues.Count == 0) return [new ContentProblem("", "DynamicFields found the content invalid.", null)];
            return [.. issues.Select(i => new ContentProblem(i.Path ?? "", i.Message ?? "Invalid.", null))];
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or NotSupportedException
                                      || (e is TaskCanceledException && !ct.IsCancellationRequested))
        {
            throw new ContentValidatorUnavailableException($"DynamicFields could not be reached: {e.Message}", e);
        }
    }
}
