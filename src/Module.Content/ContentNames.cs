namespace soft.square.net.Module.Content;

/// <summary>
/// How the pages name post kinds and units. Plain functions (no Blazor, no HTTP), so the unit tests compile this file.
/// </summary>
public static class ContentNames
{
    /// <summary>
    /// The kinds of DynamicFields' starter library, each with the English title it gives them: the pages translate that
    /// title, and offer these keys when the tenant's kinds cannot be read.
    /// </summary>
    public static readonly IReadOnlyList<(string Key, string Title)> StarterKinds =
    [
        ("post.article", "Article"), ("post.news", "News"), ("post.review", "Review"), ("post.blog", "Blog post"),
        ("post.video", "Video"), ("post.reportage", "Reportage"), ("post.photo-story", "Photo story"), ("post.other", "Other")
    ];

    /// <summary>
    /// A kind's label. A starter kind under its starter title is translated (<paramref name="translate"/>: the English
    /// text to the UI language). Any other kind, and a starter the tenant renamed, shows the title the tenant gave it
    /// (DynamicFields keeps one title per field); a kind without a title shows its key.
    /// </summary>
    public static string Kind(string? kind, string? title, Func<string, string> translate)
    {
        var starter = StarterKinds.FirstOrDefault(k => k.Key == kind).Title;
        if (starter is not null && (string.IsNullOrWhiteSpace(title) || title == starter)) return translate(starter);
        return string.IsNullOrWhiteSpace(title) ? kind ?? "" : title;
    }

    /// <summary>A unit's name in the UI language; the start of its id while Organization does not name it.</summary>
    public static string Unit(Guid id, string? nameEn, string? nameAr, string culture)
    {
        var (first, second) = culture == "ar" ? (nameAr, nameEn) : (nameEn, nameAr);
        return !string.IsNullOrWhiteSpace(first) ? first : !string.IsNullOrWhiteSpace(second) ? second : id.ToString("N")[..8];
    }
}
