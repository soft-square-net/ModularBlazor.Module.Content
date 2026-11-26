using System.Text.RegularExpressions;

namespace soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

/// <summary>
/// A post's kind is the key of a DynamicFields root post field (<c>post.article</c>, <c>post.news</c>, or any root field
/// a tenant publishes); its URL prefix comes from that field. Until wave 4 the kind was an enum, whose names
/// (<c>Article</c>, <c>News</c>, ...) are still read, from stored documents (a <see cref="PostNode"/> normalizes its
/// kind) and as input for one release, and mapped to their keys by <see cref="Normalize"/>.
/// </summary>
public static partial class PostKinds
{
    public const string Article = "post.article";
    public const string News = "post.news";
    public const string Video = "post.video";
    public const string Reportage = "post.reportage";
    public const string PhotoStory = "post.photo-story";
    public const string Other = "post.other";

    /// <summary>The longest key (DynamicFields' field key limit).</summary>
    public const int MaxLength = 64;

    /// <summary>The kinds of the old enum, by name, in its order (its numbers were the indexes).</summary>
    public static IReadOnlyList<(string Name, string Key)> Legacy { get; } =
    [
        ("Article", Article), ("News", News), ("Video", Video), ("Reportage", Reportage), ("PhotoStory", PhotoStory), ("Other", Other)
    ];

    /// <summary>
    /// The field key of a kind: an old enum name (any case) or number becomes its key; a key is trimmed and kept as it
    /// is. Null or blank stays empty (and fails <see cref="IsKey"/>).
    /// </summary>
    public static string Normalize(string? kind)
    {
        var trimmed = kind?.Trim() ?? "";
        foreach (var (name, key) in Legacy)
            if (string.Equals(trimmed, name, StringComparison.OrdinalIgnoreCase)) return key;
        if (int.TryParse(trimmed, out var index) && index >= 0 && index < Legacy.Count) return Legacy[index].Key;
        return trimmed;
    }

    /// <summary>Whether <paramref name="kind"/> is a well-formed field key (DynamicFields' format).</summary>
    public static bool IsKey(string? kind) => kind is { Length: > 0 and <= MaxLength } && KeyPattern().IsMatch(kind);

    /// <summary>The key of <paramref name="kind"/> (see <see cref="Normalize"/>), or a domain error when it is not one.</summary>
    public static string Require(string? kind)
    {
        var key = Normalize(kind);
        return IsKey(key)
            ? key
            : throw new DomainException($"'{kind}' is not a post kind: a DynamicFields root field key such as {Article} (at most {MaxLength} characters).");
    }

    [GeneratedRegex("^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$")]
    private static partial Regex KeyPattern();
}

/// <summary>
/// A kind's static URL prefix (DynamicFields' root field <c>urlPrefix</c>): lowercase ASCII words joined by '-', in
/// '/'-separated segments (<c>news</c>, <c>ar/news</c>), with no leading or trailing slash, at most 200 characters.
/// </summary>
public static partial class UrlPrefixes
{
    public const int MaxLength = 200;

    public static bool IsValid(string? prefix) => prefix is { Length: > 0 and <= MaxLength } && Pattern().IsMatch(prefix);

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*(?:/[a-z0-9]+(?:-[a-z0-9]+)*)*$")]
    private static partial Regex Pattern();
}
