using System.Text.RegularExpressions;

namespace soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

/// <summary>
/// A post's reference to a shared content (<c>ContentItem</c>), in reading order. <see cref="PinnedRevision"/> is the
/// accepted revision the post was scheduled or published with; null until then.
/// </summary>
public sealed class PostContent : EntityBase<int>
{
    private PostContent() { } // EF Core

    internal PostContent(Guid contentId, int order, bool required)
    {
        ContentId = contentId;
        Order = order;
        Required = required;
    }

    public int PostId { get; private set; }
    public Guid ContentId { get; private set; }
    public int Order { get; internal set; }
    /// <summary>A required content must be accepted before the post is published; an optional one is left out until it is.</summary>
    public bool Required { get; internal set; }
    public int? PinnedRevision { get; internal set; }
}

/// <summary>What a post's contents should be, in order (an attach, detach, reorder, or a whole-document edit).</summary>
public sealed record ContentRef(Guid ContentId, bool Required = true);

/// <summary>What a publication goes live with: the accepted revision of each content, and the document assembled from them.</summary>
public sealed record PublishPlan(IReadOnlyDictionary<Guid, int> Pins, PostDocument Document);

/// <summary>A post's URL slug in one culture (unique per culture in the tenant).</summary>
public sealed partial class PostSlug : EntityBase<int>
{
    public const int MaxLength = 200;
    public const int MaxCultures = 20;

    private PostSlug() { } // EF Core

    internal PostSlug(string culture, string slug)
    {
        Culture = culture;
        Slug = slug;
    }

    public int PostId { get; private set; }
    public string Culture { get; private set; } = "";
    public string Slug { get; internal set; } = "";

    /// <summary>Lowercase letters of any script, digits and single '-' between them (at most 200).</summary>
    public static bool IsSlug(string? slug) => slug is { Length: > 0 and <= MaxLength } && Pattern().IsMatch(slug);

    [GeneratedRegex(@"^[\p{Ll}\p{Lo}\p{Nd}][\p{Ll}\p{Lo}\p{Nd}\p{Mn}]*(-[\p{Ll}\p{Lo}\p{Nd}][\p{Ll}\p{Lo}\p{Nd}\p{Mn}]*)*$")]
    private static partial Regex Pattern();
}

/// <summary>Who edits a post, which decides what an edit does (see <see cref="Post.Edit"/>).</summary>
public enum PostEditor { Owner, Reviewer, Desk }

/// <summary>
/// One edit of a post: any of its text, its document (root values plus the assembled contents), its contents, its slugs.
/// </summary>
public sealed record PostChange(string? Title = null, string? Body = null, PostDocument? Document = null,
    IReadOnlyList<ContentRef>? Contents = null, IReadOnlyDictionary<string, string>? Slugs = null);

/// <summary>One of a post's categories (the primary one included). Unique per post.</summary>
public sealed class PostCategory : EntityBase<int>
{
    private PostCategory() { } // EF Core

    internal PostCategory(string categoryId) => CategoryId = categoryId;

    public int PostId { get; private set; }
    public string CategoryId { get; private set; } = "";
}
