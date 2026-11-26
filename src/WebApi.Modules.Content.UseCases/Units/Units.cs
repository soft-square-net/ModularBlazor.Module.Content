using Microsoft.Extensions.Logging;
using soft.square.net.Organization.Contracts;
using soft.square.net.WebApi.Modules.Content.Core.Common;
using soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Units;

/// <summary>An Organization unit as Content needs it: its path (for the scopes) and the post categories it serves.</summary>
public sealed record UnitInfo(Guid Id, string Path, string TypeKey, string NameEn, string NameAr, IReadOnlyList<string> Categories);

/// <summary>
/// The current tenant's Organization units, read from the deployment's directory (Organization's local read API, over HTTP:
/// <see cref="HttpUnitDirectory"/>), cached per tenant for a short time and forgotten on <c>org.unit-*</c> events.
/// </summary>
public interface IUnitLookup
{
    /// <summary>The unit; null when the tenant has no such unit. Throws <see cref="UnitLookupUnavailableException"/> when Organization cannot answer.</summary>
    Task<UnitInfo?> FindAsync(Guid unitId, CancellationToken ct);

    /// <summary>Drops what is cached about the unit (an <c>org.unit-*</c> event named it).</summary>
    void Forget(Guid unitId);
}

/// <summary>Organization's directory could not be reached, or refused the call.</summary>
public sealed class UnitLookupUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Whether a unit serves a category: its categories list it, or are the wildcard <c>*</c> (decision D1: a Section that
/// serves every category, such as Multimedia or SEO).
/// </summary>
public static class UnitCategoryRules
{
    /// <summary>The wildcard: every category.</summary>
    public const string All = "*";

    // TODO: use Organization.Contracts' UnitCategories.Covers once a package with it is released (it is on Organization's
    // main, PR #7, after 1.0.0-preview.1); this is the same rule.
    public static bool Covers(IEnumerable<string> unitCategories, string categoryId)
    {
        var list = unitCategories as IReadOnlyCollection<string> ?? [.. unitCategories];
        return list.Contains(All, StringComparer.Ordinal) || list.Contains(categoryId, StringComparer.Ordinal);
    }

    /// <summary>The unit serves at least one of <paramref name="categories"/>.</summary>
    public static bool CoversAny(IEnumerable<string> unitCategories, IEnumerable<string> categories)
    {
        var list = unitCategories as IReadOnlyCollection<string> ?? [.. unitCategories];
        return categories.Any(c => Covers(list, c));
    }
}

/// <summary>
/// The rules on the unit of a post or a content: the unit must exist in the tenant, and its categories must cover one of the
/// post's categories (the content's category), <c>*</c> covering all. Who may set it (<c>content.unit.assign</c>) is checked
/// by the use case.
/// </summary>
public sealed class UnitRules(IUnitLookup units, ILoggerFactory loggers)
{
    private readonly ILogger _log = loggers.CreateLogger("soft.square.net.WebApi.Modules.Content.UseCases.Units");

    /// <summary>The unit, when it serves one of <paramref name="categories"/>; 400 when it is unknown, 409 when it serves none, 503 when Organization is down.</summary>
    public async Task<Result<UnitInfo>> ServingAsync(Guid unitId, IReadOnlyCollection<string> categories, string what, CancellationToken ct)
    {
        var found = await FindAsync(unitId, ct);
        if (!found.IsSuccess) return found;
        var unit = found.Value;
        if (!UnitCategoryRules.CoversAny(unit.Categories, categories))
            return Result<UnitInfo>.Conflict(
                $"Unit {UnitPathRules.Id(unitId)} ({unit.NameEn}) does not serve {what}: its categories are " +
                $"[{string.Join(", ", unit.Categories)}], and none of them is {string.Join(" or ", categories)} (or '*').");
        return unit;
    }

    /// <summary>The unit of a post may be set when it serves one of the post's categories.</summary>
    public Task<Result<UnitInfo>> ForPostAsync(Post post, Guid unitId, CancellationToken ct) =>
        ServingAsync(unitId, post.Categories.ToArray(), $"any category of post {post.Id}", ct);

    /// <summary>The unit of a content may be set when it serves the content's category.</summary>
    public Task<Result<UnitInfo>> ForContentAsync(ContentItem content, Guid unitId, CancellationToken ct) =>
        ServingAsync(unitId, [content.CategoryId], $"the content's category '{content.CategoryId}'", ct);

    private async Task<Result<UnitInfo>> FindAsync(Guid unitId, CancellationToken ct)
    {
        try
        {
            return await units.FindAsync(unitId, ct) is { } unit
                ? unit
                : Result<UnitInfo>.Invalid(new ValidationError("unitId", $"The tenant has no Organization unit {UnitPathRules.Id(unitId)}."));
        }
        catch (UnitLookupUnavailableException e)
        {
            _log.LogWarning(e, "Unit {Unit} could not be read from Organization: {Reason}", unitId, e.Message);
            return Result<UnitInfo>.Unavailable($"The Organization unit could not be read ({e.Message}); try again later.");
        }
    }
}
