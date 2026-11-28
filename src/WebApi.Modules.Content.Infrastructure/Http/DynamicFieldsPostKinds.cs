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

    /// <summary>What Content reads of DynamicFields' field version (<c>FieldVersionDto</c>): the keys of its values.</summary>
    private sealed record FieldVersionWire(FieldWire? Field);
    private sealed record FieldWire(List<PropertyWire>? Properties);
    private sealed record PropertyWire(string? Key);

    public static string VersionPath(string key, int version) =>
        $"/api/dynamicfields/fields/{Uri.EscapeDataString(key)}/versions/{version.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    /// <summary>One entry of DynamicFields' list (its <c>RootPostFieldDto</c>).</summary>
    private sealed record RootPostFieldWire(string? Key, string? Title, string? Description, string? SchemaType, string? UrlPrefix, int Version);

    public async Task<IReadOnlyList<PostKindInfo>> ListAsync(CancellationToken ct)
    {
        var cacheKey = $"content-post-kinds|{TenantId()}";
        if (cache.TryGetValue(cacheKey, out IReadOnlyList<PostKindInfo>? hit) && hit is not null) return hit;

        var wire = await GetAsync<List<RootPostFieldWire>>(Path, ct);
        IReadOnlyList<PostKindInfo> kinds = [.. (wire ?? [])
            .Where(f => !string.IsNullOrWhiteSpace(f.Key) && !string.IsNullOrWhiteSpace(f.UrlPrefix))
            .Select(f => new PostKindInfo(f.Key!, string.IsNullOrWhiteSpace(f.Title) ? f.Key! : f.Title!, f.UrlPrefix!, f.SchemaType, f.Version))];
        Keep(cacheKey, kinds);
        return kinds;
    }

    public async Task<IReadOnlyList<string>> ValueKeysAsync(string key, int version, CancellationToken ct)
    {
        var cacheKey = $"content-post-kind-values|{TenantId()}|{key}|{version}";
        if (cache.TryGetValue(cacheKey, out IReadOnlyList<string>? hit) && hit is not null) return hit;

        var wire = await GetAsync<FieldVersionWire>(VersionPath(key, version), ct);
        if (wire?.Field?.Properties is not { } properties)
            throw new PostKindCatalogUnavailableException($"DynamicFields answered no values for {key} version {version}");
        IReadOnlyList<string> keys = [.. properties.Select(p => p.Key).OfType<string>()];
        Keep(cacheKey, keys);
        return keys;
    }

    private string? TenantId() =>
        accessor.HttpContext?.Request.Headers[TenancyNames.Header].ToString() is { Length: > 0 } header ? header : tenant.TenantId;

    private void Keep<T>(string key, T value)
    {
        var seconds = options.Value.PostKindCacheSeconds;
        if (seconds > 0)
            cache.Set(key, value, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(seconds), Size = 1 });
    }

    private async Task<T?> GetAsync<T>(string path, CancellationToken ct)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.ApiBaseUrl)) throw new PostKindCatalogUnavailableException("Content:ApiBaseUrl is not set");
        using var request = new HttpRequestMessage(HttpMethod.Get, o.ApiBaseUrl.TrimEnd('/') + path);
        if (accessor.HttpContext?.Request.Headers.Authorization.ToString() is { Length: > 0 } authorization)
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
        if (TenantId() is { } tenantId) request.Headers.TryAddWithoutValidation(TenancyNames.Header, tenantId);

        try
        {
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                throw new PostKindCatalogUnavailableException($"DynamicFields answered {(int)response.StatusCode} {response.ReasonPhrase}");
            return await response.Content.ReadFromJsonAsync<T>(Json, ct);
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or NotSupportedException
                                      || (e is TaskCanceledException && !ct.IsCancellationRequested))
        {
            throw new PostKindCatalogUnavailableException($"DynamicFields could not be reached: {e.Message}", e);
        }
    }
}
