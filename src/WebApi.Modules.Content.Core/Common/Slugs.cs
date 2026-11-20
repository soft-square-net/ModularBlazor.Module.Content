using System.Text.RegularExpressions;

namespace soft.square.net.WebApi.Modules.Content.Core.Common;

/// <summary>Validates the slug ids of presses and categories (they appear in grant scopes).</summary>
internal static partial class Slugs
{
    public static string Require(string value, string what) =>
        value is not null && Slug().IsMatch(value) ? value : throw new DomainException($"A {what} id is lowercase letters, digits and '-' (at most 64).");

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$")]
    private static partial Regex Slug();
}
