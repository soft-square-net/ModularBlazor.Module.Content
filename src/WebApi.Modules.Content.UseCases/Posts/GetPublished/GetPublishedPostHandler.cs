using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate.Specifications;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.GetPublished;

public sealed class GetPublishedPostHandler(IReadRepository<Post> reader) : IQueryHandler<GetPublishedPostQuery, Result<PublishedPost>>
{
    private const string Legacy = "legacy-";

    /// <summary>A culture and its parents: ar-EG, then ar.</summary>
    private static IEnumerable<string> CultureChain(string? culture)
    {
        for (var c = culture?.Trim(); !string.IsNullOrEmpty(c); c = c.LastIndexOf('-') is var dash and > 0 ? c[..dash] : null)
            yield return c;
    }

    public async ValueTask<Result<PublishedPost>> Handle(GetPublishedPostQuery query, CancellationToken ct)
    {
        var publicId = query.PublicId;
        var post = publicId.StartsWith(Legacy, StringComparison.Ordinal) && int.TryParse(publicId.AsSpan(Legacy.Length), out var legacyId)
            ? await reader.FirstOrDefaultAsync(new PublishedPostByLegacyIdSpec(legacyId), ct)
            : await reader.FirstOrDefaultAsync(new PublishedPostByPublicIdSpec(publicId), ct);
        if (post?.PublishedAt is not { } publishedAt) return Result<PublishedPost>.NotFound("Published Post was not found.");

        var document = PostDetails.ReadDocument(post.PublishedDocumentJson, post.PublishedTitle ?? post.Title, post.PublishedBody ?? post.Body,
            "The published Post document is invalid.");
        var translations = post.PublishedTranslations;
        var served = PostTranslationRules.Chain(query.Culture, translations.Select(t => t.Language));
        var chain = served.Select(language => translations.First(t => t.Language == language)).ToList();
        return new PublishedPost(post.Id, post.StablePublicId, PostDetails.Permalink(post.StablePublicId), post.PostKind, post.PressId,
            post.PrimaryCategoryId, post.PublishedTitle ?? post.Title, publishedAt, document.SchemaId, document.SchemaVersion,
            PostTranslationRules.Apply(document, chain), translations.Select(t => t.Language).ToList(), served,
            post.PublishedUrlPrefix, post.CanonicalPath([.. served, .. CultureChain(query.Culture)]));
    }
}
