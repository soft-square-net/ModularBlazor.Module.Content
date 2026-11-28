using System.Linq.Expressions;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts;

/// <summary>
/// The rights checks the Post use cases share. Endpoint-level checks are coarse (the caller holds the permission
/// somewhere); every use case checks the permission for this Post's scopes (its press, its categories, and its unit with
/// the unit's ancestors), using the
/// caller's grants as they are right now: shift grants count only while the shift runs, so access ends on the clock.
/// </summary>
public sealed class PostAccess(IAccessScopes access, TimeProvider clock)
{
    public DateTimeOffset Now => clock.GetUtcNow();

    /// <summary>The caller's user id. Every Post endpoint but the public permalink requires a signed-in caller.</summary>
    public string Me => access.UserId ?? throw new ContentAccessDeniedException("Sign in first.");

    public async Task<Result> RequireAsync(string permission, IReadOnlyCollection<string> scopes, string what, CancellationToken ct) =>
        await access.HasPermissionAsync(permission, scopes, ct)
            ? Result.Success()
            : Result.Forbidden($"You may not {what} ({permission}).");

    /// <summary>Whether the caller holds the permission on these scopes now.</summary>
    public Task<bool> HasAsync(string permission, IReadOnlyCollection<string> scopes, CancellationToken ct) =>
        access.HasPermissionAsync(permission, scopes, ct);

    /// <summary>
    /// Where the caller holds any of the permissions now: the whole tenant, or these presses, categories and units (N-form
    /// ids; a unit covers everything below it). All empty: nowhere.
    /// </summary>
    public async Task<PermissionPlaces> WhereAsync(string[] permissions, CancellationToken ct)
    {
        var scopes = new HashSet<string>();
        foreach (var permission in permissions) scopes.UnionWith(await access.ScopesAsync(permission, ct));
        if (scopes.Contains(AccessScope.Tenant)) return new(true, [], [], []);
        return new(false, Of(scopes, "press:"), Of(scopes, "category:"), Of(scopes, "unit:"));
    }

    private static string[] Of(IEnumerable<string> scopes, string kind) =>
        [.. scopes.Where(s => s.StartsWith(kind, StringComparison.Ordinal) && s.Length > kind.Length).Select(s => s[kind.Length..]).Distinct()];

    /// <summary>
    /// The permissions that let a caller see posts and contents they neither own nor work on (the workflow's
    /// <c>content.unit.assign</c> too: it files them).
    /// </summary>
    public static readonly string[] SeeingPermissions =
        [ContentPermissions.Accept, ContentPermissions.Assign, ContentPermissions.Return, ContentPermissions.Publish, ContentPermissions.Editor,
         ContentPermissions.Manage, ContentPermissions.UnitAssign];

    /// <summary>Whether another user holds the permission on these scopes now (grants only, no platform role).</summary>
    public Task<bool> UserHasAsync(string userId, string permission, IReadOnlyCollection<string> scopes, CancellationToken ct) =>
        access.UserHasPermissionAsync(userId, permission, scopes, ct);

    /// <summary>For work on the whole tenant: the permission must cover all of it.</summary>
    public async Task<Result> RequireTenantWideAsync(string permission, string message, CancellationToken ct) =>
        (await access.ScopesAsync(permission, ct)).Contains(AccessScope.Tenant) ? Result.Success() : Result.Forbidden(message);

    /// <summary>Desk on shift; publishing your own or self-reviewed Post also needs the standing editor right on its category.</summary>
    public async Task<Result> RequirePublisherAsync(Post post, CancellationToken ct)
    {
        var desk = await RequireAsync(ContentPermissions.Publish, post.Scopes, "publish in this category now", ct);
        if (!desk.IsSuccess) return desk;
        if ((post.OwnerId == Me || post.ReviewerId == Me)
            && !await access.HasPermissionAsync(ContentPermissions.Editor, [.. post.Categories.Select(AccessScope.Category)], ct))
            return Result.Forbidden("You wrote or reviewed this Post: publishing it yourself needs the standing editor right on its category.");
        return Result.Success();
    }

    /// <summary>Whether the caller may see Posts in these scopes without owning or reviewing them.</summary>
    public async Task<bool> SeesAsync(IReadOnlyCollection<string> scopes, CancellationToken ct)
    {
        foreach (var permission in SeeingPermissions)
            if (await access.HasPermissionAsync(permission, scopes, ct)) return true;
        return false;
    }

    /// <summary>
    /// Rows in any scope where the caller holds one of the permissions now (a press, any of a post's categories, its unit or
    /// a unit above it); null when none at all.
    /// </summary>
    public async Task<Expression<Func<Post, bool>>?> ScopeFilterAsync(string[] permissions, CancellationToken ct)
    {
        var places = await WhereAsync(permissions, ct);
        if (places.Tenant) return _ => true;
        if (places.IsNowhere) return null;
        var presses = places.Presses;
        var cats = places.Categories;
        Expression<Func<Post, bool>> filter = a => presses.Contains(a.PressId) || cats.Contains(a.PrimaryCategoryId)
            || a.CategoryLinks.Any(c => cats.Contains(c.CategoryId));
        return UnitFilter.Or(filter, places.Units, a => a.UnitPath);
    }
}

/// <summary>Where a caller holds a permission: the whole tenant, or these presses, categories and units (N-form ids).</summary>
public sealed record PermissionPlaces(bool Tenant, string[] Presses, string[] Categories, string[] Units)
{
    public bool IsNowhere => !Tenant && Presses.Length == 0 && Categories.Length == 0 && Units.Length == 0;
}

/// <summary>
/// The list filter for unit grants: a row whose unit path contains <c>/{id}</c> for a granted unit (its unit is that unit
/// or below it). Built as one OR of LIKE tests, which every engine translates.
/// </summary>
public static class UnitFilter
{
    /// <summary>The most unit grants put in one filter (more are unusual; the rest are left out of lists, never of checks).</summary>
    public const int MaxUnits = 50;

    public static Expression<Func<T, bool>> Or<T>(Expression<Func<T, bool>> filter, IReadOnlyCollection<string> units, Expression<Func<T, string?>> path)
    {
        if (units.Count == 0) return filter;
        var row = filter.Parameters[0];
        var value = new ParameterReplacer(path.Parameters[0], row).Visit(path.Body);
        var contains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
        Expression body = filter.Body;
        foreach (var unit in units.Take(MaxUnits))
        {
            var needle = Expression.Constant("/" + unit);
            body = Expression.OrElse(body, Expression.AndAlso(
                Expression.NotEqual(value, Expression.Constant(null, typeof(string))),
                Expression.Call(value, contains, needle)));
        }
        return Expression.Lambda<Func<T, bool>>(body, row);
    }

    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }
}
