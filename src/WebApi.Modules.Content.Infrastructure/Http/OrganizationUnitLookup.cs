using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using soft.square.net.Organization.Contracts;
using soft.square.net.WebApi.Contracts.Tenancy;
using soft.square.net.WebApi.Modules.Content.UseCases;
using soft.square.net.WebApi.Modules.Content.UseCases.Units;

namespace soft.square.net.WebApi.Modules.Content.Infrastructure.Http;

/// <summary>
/// <see cref="IUnitLookup"/> over Organization's local read API (the deployment's replica of the org tree), through the
/// gateway (<c>{Content:ApiBaseUrl}/api/organization/local/units/...</c>) with <see cref="HttpUnitDirectory"/>, never in
/// process. The call carries the caller's access token and tenant. A unit is cached per tenant for
/// <see cref="ContentOptions.UnitCacheSeconds"/> and forgotten on <c>org.unit-*</c> events; failures and unknown units are
/// not cached. A unit outside the tenant (one of its root's read-only ancestors) counts as unknown: posts are filed in the
/// tenant's own units.
/// </summary>
public sealed class OrganizationUnitLookup(IHttpClientFactory clients, IHttpContextAccessor accessor, ITenantContext tenant, IMemoryCache cache,
    IOptions<ContentOptions> options) : IUnitLookup
{
    public const string ClientName = "content-organization";
    public const string Route = "/api/organization/";

    public async Task<UnitInfo?> FindAsync(Guid unitId, CancellationToken ct)
    {
        var incoming = accessor.HttpContext?.Request;
        var tenantId = incoming?.Headers[TenancyNames.Header].ToString() is { Length: > 0 } header ? header : tenant.TenantId;
        var key = Key(tenantId, unitId);
        if (cache.TryGetValue(key, out UnitInfo? hit) && hit is not null) return hit;

        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.ApiBaseUrl)) throw new UnitLookupUnavailableException("Content:ApiBaseUrl is not set");
        using var http = clients.CreateClient(ClientName);
        http.BaseAddress = new Uri(o.ApiBaseUrl.TrimEnd('/') + Route);
        if (incoming?.Headers.Authorization.ToString() is { Length: > 0 } authorization)
            http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authorization);
        if (tenantId is not null) http.DefaultRequestHeaders.TryAddWithoutValidation(TenancyNames.Header, tenantId);
        var directory = new HttpUnitDirectory(http);

        UnitInfo? unit;
        try
        {
            var found = await directory.GetAsync(unitId, ct);
            if (found is null || !found.InTenant) return null;
            var categories = await directory.CategoriesOfAsync(unitId, ct);
            unit = new UnitInfo(found.Id, found.Path, found.TypeKey, found.NameEn, found.NameAr, categories);
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or NotSupportedException
                                      || (e is TaskCanceledException && !ct.IsCancellationRequested))
        {
            throw new UnitLookupUnavailableException($"Organization could not be reached: {e.Message}", e);
        }
        if (o.UnitCacheSeconds > 0)
            cache.Set(key, unit, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(o.UnitCacheSeconds), Size = 1 });
        return unit;
    }

    public void Forget(Guid unitId) => cache.Remove(Key(tenant.TenantId, unitId));

    private static string Key(string? tenantId, Guid unitId) => $"content-unit|{tenantId}|{unitId:N}";
}
