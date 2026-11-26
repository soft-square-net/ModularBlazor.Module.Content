using System.Text.RegularExpressions;

namespace soft.square.net.WebApi.Modules.Content.Core.Common;

/// <summary>
/// Organization unit paths and scopes as Content stores and checks them. The same formats as Organization's
/// <c>UnitPaths</c> and <c>UnitScopes</c> (Organization.Contracts; the domain references no package): a path is
/// <c>/{rootId}/.../{unitId}</c> with 32-digit (<c>"N"</c>) ids, and the scopes of a unit are <c>unit:{id}</c> for it and
/// every ancestor, so a grant on a unit covers everything below it.
/// </summary>
public static partial class UnitPathRules
{
    /// <summary>The longest path stored (about 15 levels).</summary>
    public const int MaxLength = 512;

    /// <summary>The text of a unit id in a path and in a scope.</summary>
    public static string Id(Guid unitId) => unitId.ToString("N");

    /// <summary>The scope of one unit: <c>unit:{id}</c>.</summary>
    public static string Scope(Guid unitId) => $"unit:{Id(unitId)}";

    /// <summary>The scopes of the unit at <paramref name="path"/> and of all its ancestors, root first; none without a path.</summary>
    public static IReadOnlyList<string> Scopes(string? path) =>
        string.IsNullOrEmpty(path) ? [] : [.. path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(id => $"unit:{id}")];

    /// <summary>
    /// The path to store with a unit: null without a unit; otherwise <paramref name="path"/>, which must be a unit path that
    /// ends with the unit's own id.
    /// </summary>
    public static string? Require(Guid? unitId, string? path)
    {
        if (unitId is null) return null;
        if (unitId == Guid.Empty) throw new DomainException("A unit id cannot be empty.");
        var text = path?.Trim() ?? "";
        if (text.Length > MaxLength || !Pattern().IsMatch(text) || !text.EndsWith("/" + Id(unitId.Value), StringComparison.Ordinal))
            throw new DomainException($"'{path}' is not the path of unit {Id(unitId.Value)} (/{{root}}/.../{{unit}}, 32-digit ids).");
        return text;
    }

    /// <summary>
    /// <paramref name="path"/> after a move of the unit at <paramref name="oldPath"/> to <paramref name="newPath"/>; null when
    /// <paramref name="path"/> is not that unit or below it.
    /// </summary>
    public static string? Rebase(string? path, string oldPath, string newPath)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(oldPath)) return null;
        if (!(path == oldPath || path.StartsWith(oldPath + "/", StringComparison.Ordinal))) return null;
        var rebased = newPath + path[oldPath.Length..];
        return rebased == path || rebased.Length > MaxLength ? null : rebased;
    }

    [GeneratedRegex("^(/[0-9a-f]{32})+$")]
    private static partial Regex Pattern();
}
