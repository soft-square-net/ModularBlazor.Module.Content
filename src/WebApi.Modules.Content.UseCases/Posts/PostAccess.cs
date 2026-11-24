using System.Linq.Expressions;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts;

/// <summary>
/// The rights checks the Post use cases share. Endpoint-level checks are coarse (the caller holds the permission
/// somewhere); every use case checks the permission for this Post's scopes (its press and category), using the
/// caller's grants as they are right now: shift grants count only while the shift runs, so access ends on the clock.
/// </summary>
public sealed class PostAccess(IAccessScopes access, IGrantStore grants, TimeProvider clock)
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
    /// Where the caller holds any of the permissions now: the whole tenant, or these presses and categories (both empty: nowhere).
    /// </summary>
    public async Task<(bool Tenant, string[] Presses, string[] Categories)> WhereAsync(string[] permissions, CancellationToken ct)
    {
        var scopes = new HashSet<string>();
        foreach (var permission in permissions) scopes.UnionWith(await access.ScopesAsync(permission, ct));
        if (scopes.Contains(AccessScope.Tenant)) return (true, [], []);
        return (false, [.. scopes.Where(s => s.StartsWith("press:")).Select(s => s["press:".Length..])],
            [.. scopes.Where(s => s.StartsWith("category:")).Select(s => s["category:".Length..])]);
    }

    /// <summary>The permissions that let a caller see posts and contents they neither own nor work on.</summary>
    public static readonly string[] SeeingPermissions =
        [ContentPermissions.Accept, ContentPermissions.Assign, ContentPermissions.Return, ContentPermissions.Publish, ContentPermissions.Editor, ContentPermissions.Manage];

    /// <summary>For the automation endpoints: the permission must cover the whole tenant.</summary>
    public async Task<Result> RequireTenantWideAsync(string permission, string message, CancellationToken ct) =>
        (await access.ScopesAsync(permission, ct)).Contains(AccessScope.Tenant) ? Result.Success() : Result.Forbidden(message);

    public async Task<Result> RequireShiftPermissionAsync(string permission, IReadOnlyCollection<string> scopes, string shiftId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(shiftId)) return Result.Forbidden("An active shift is required for this action.");
        var now = Now;
        var valid = (await grants.ListAsync(Me, $"shift:{shiftId}", ct)).Any(g =>
            g.Permission == permission && g.IsActiveAt(now) && g.Covers(scopes));
        return valid ? Result.Success() : Result.Forbidden($"You do not hold {permission} in active shift '{shiftId}' for this category.");
    }

    /// <summary>Desk on shift; publishing your own or self-reviewed Post also needs the standing editor right on its category.</summary>
    public async Task<Result> RequirePublisherAsync(Post post, CancellationToken ct)
    {
        var desk = await RequireAsync(ContentPermissions.Publish, post.Scopes, "publish in this category now", ct);
        if (!desk.IsSuccess) return desk;
        if ((post.OwnerId == Me || post.ReviewerId == Me) && !await access.HasPermissionAsync(ContentPermissions.Editor, [AccessScope.Category(post.CategoryId)], ct))
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

    /// <summary>Rows in any scope where the caller holds one of the permissions now; null when none at all.</summary>
    public async Task<Expression<Func<Post, bool>>?> ScopeFilterAsync(string[] permissions, CancellationToken ct)
    {
        var scopes = new HashSet<string>();
        foreach (var permission in permissions) scopes.UnionWith(await access.ScopesAsync(permission, ct));
        if (scopes.Contains(AccessScope.Tenant)) return _ => true;
        var presses = scopes.Where(s => s.StartsWith("press:")).Select(s => s["press:".Length..]).ToArray();
        var cats = scopes.Where(s => s.StartsWith("category:")).Select(s => s["category:".Length..]).ToArray();
        if (presses.Length == 0 && cats.Length == 0) return null;
        return a => presses.Contains(a.PressId) || cats.Contains(a.CategoryId);
    }
}
