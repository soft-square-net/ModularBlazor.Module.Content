using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using soft.square.net.WebApi.Contracts.Tenancy;
using soft.square.net.WebApi.Modules.Content.UseCases;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Kinds;

namespace soft.square.net.WebApi.Modules.Content.Infrastructure.Http;

/// <summary>
/// <see cref="IPostKindCatalog"/> over DynamicFields' API, through the gateway (<c>Content:ApiBaseUrl</c>), never in
/// process (modules load in isolated contexts): <c>GET /api/dynamicfields/catalog/root-post-fields</c>
/// (<c>DynamicFields_ListRootPostFields</c>, permission <c>dynamicfields.read</c>). The call carries the caller's access
/// token and tenant. The list is cached per tenant for <see cref="ContentOptions.PostKindCacheSeconds"/>; failures are not
/// cached.
/// </summary>
public sealed class DynamicFieldsPostKinds(HttpClient http, IHttpContextAccessor accessor, ITenantContext tenant, IMemoryCache cache,
    IOptions<ContentOptions> options) : IPostKindCatalog
{
    public const string Path = "/api/dynamicfields/catalog/root-post-fields";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>One entry of DynamicFields' list (its <c>RootPostFieldDto</c>).</summary>
    private sealed record RootPostFieldWire(string? Key, string? Title, string? Description, string? SchemaType, string? UrlPrefix, int Version);

    public async Task<IReadOnlyList<PostKindInfo>> ListAsync(CancellationToken ct)
    {
        var incoming = accessor.HttpContext?.Request;
        var tenantId = incoming?.Headers[TenancyNames.Header].ToString() is { Length: > 0 } header ? header : tenant.TenantId;
        var key = $"content-post-kinds|{tenantId}";
        if (cache.TryGetValue(key, out IReadOnlyList<PostKindInfo>? hit) && hit is not null) return hit;

        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.ApiBaseUrl)) throw new PostKindCatalogUnavailableException("Content:ApiBaseUrl is not set");
        using var request = new HttpRequestMessage(HttpMethod.Get, o.ApiBaseUrl.TrimEnd('/') + Path);
        if (incoming?.Headers.Authorization.ToString() is { Length: > 0 } authorization)
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
        if (tenantId is not null) request.Headers.TryAddWithoutValidation(TenancyNames.Header, tenantId);

        List<RootPostFieldWire>? wire;
        try
        {
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                throw new PostKindCatalogUnavailableException($"DynamicFields answered {(int)response.StatusCode} {response.ReasonPhrase}");
            wire = await response.Content.ReadFromJsonAsync<List<RootPostFieldWire>>(Json, ct);
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or NotSupportedException
                                      || (e is TaskCanceledException && !ct.IsCancellationRequested))
        {
            throw new PostKindCatalogUnavailableException($"DynamicFields could not be reached: {e.Message}", e);
        }

        IReadOnlyList<PostKindInfo> kinds = [.. (wire ?? [])
            .Where(f => !string.IsNullOrWhiteSpace(f.Key) && !string.IsNullOrWhiteSpace(f.UrlPrefix))
            .Select(f => new PostKindInfo(f.Key!, string.IsNullOrWhiteSpace(f.Title) ? f.Key! : f.Title!, f.UrlPrefix!, f.SchemaType))];
        if (o.PostKindCacheSeconds > 0)
            cache.Set(key, kinds, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(o.PostKindCacheSeconds), Size = 1 });
        return kinds;
    }
}
