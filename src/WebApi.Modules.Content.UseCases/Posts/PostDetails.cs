using System.Text.Json;
using Microsoft.Extensions.Options;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;
using soft.square.net.WebApi.Modules.Content.Core.ContentAggregate.Specifications;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate.Specifications;
using soft.square.net.WebApi.Modules.Content.UseCases.Contents;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Kinds;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts;

/// <summary>Reads one Post as the caller may see it, and maps it to <see cref="PostDetail"/>.</summary>
public sealed class PostDetails(IReadRepository<Post> reader, IReadRepository<ContentItem> contents, PostAccess access, IOptions<ContentOptions> options,
    PostKindRules kinds)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The Post, if the caller owns it, reviews it, or holds a content permission on its scopes; otherwise not found.</summary>
    public async Task<Result<PostDetail>> GetAsync(int id, CancellationToken ct)
    {
        var post = await reader.FirstOrDefaultAsync(new PostDetailSpec(id), ct);
        if (post is null) return NotFound(id);
        if (!await SeesAsync(post, ct)) return NotFound(id);
        var ids = post.ContentLinks.Select(c => c.ContentId).ToList();
        var items = ids.Count == 0 ? [] : (await contents.ListAsync(new ContentsByIdsSpec(ids), ct) ?? []).ToDictionary(c => c.Id);
        var prefix = await kinds.UrlPrefixOrNullAsync(post.PostKind, ct) ?? post.PublishedUrlPrefix;
        return ToDetail(post, items, prefix);
    }

    /// <summary>Whether the caller may see the post (for its feed).</summary>
    public async Task<bool> SeesAsync(int id, CancellationToken ct) =>
        await reader.FirstOrDefaultAsync(new PostByIdReadSpec(id), ct) is { } post && await SeesAsync(post, ct);

    private async Task<bool> SeesAsync(Post post, CancellationToken ct) =>
        post.OwnerId == access.Me || post.AssigneeId == access.Me || await access.SeesAsync(post.Scopes, ct);

    public static Result<PostDetail> NotFound(int id) => Result<PostDetail>.NotFound($"Post {id} was not found.");

    public static string Permalink(string publicId) => $"/api/content/public/{Uri.EscapeDataString(publicId)}";

    /// <summary>A stored document, or the legacy title and body as one when none was stored.</summary>
    public static PostDocument ReadDocument(string? json, string title, string body, string invalidMessage) =>
        string.IsNullOrWhiteSpace(json) || json == "{}"
            ? PostDocument.FromLegacy(title, body)
            : JsonSerializer.Deserialize<PostDocument>(json, Json) ?? throw new InvalidOperationException(invalidMessage);

    private static PostContentDto ToDto(PostContent link, ContentItem? c) => c is null
        ? new(link.ContentId, link.Order, link.Required, link.PinnedRevision, "", "", 0, "", null, null, 0, null, null, 0, true, false, null)
        : new(link.ContentId, link.Order, link.Required, link.PinnedRevision, c.NodeId, c.FieldKey, c.FieldVersion, c.Stage, c.SectionId,
            c.AssigneeId, c.Revision, c.AcceptedRevision, c.ReviewNote, c.Version, c.IsArchived,
            link.PinnedRevision is { } pinned && c.AcceptedRevision is { } accepted && accepted > pinned, c.UnitId);

    private PostDetail ToDetail(Post a, IReadOnlyDictionary<Guid, ContentItem> items, string? urlPrefix) => new(a.Id, a.PostKind, a.PressId, a.PrimaryCategoryId, a.Title, a.Body, a.OwnerId, a.State, a.Version,
        a.CreatedAt, a.UpdatedAt, a.SubmittedAt, a.OwnerMayEdit(access.Now, options.Value.OwnerEditWindow), a.AcceptedBy,
        a.AcceptedAt, a.AcceptedShiftId, a.AssigneeId,
        a.AssignedShiftId, a.ReviewerId, a.ScheduledFor, a.ScheduledBy, a.PublishedAt, a.PublishedBy,
        a.PublishedTitle, a.PublishedBody, a.TransferredFromShift,
        a.History.OrderBy(e => e.At).ThenBy(e => e.Id).Select(e => new PostEventDto(e.At, e.Actor, e.Action, e.State, e.Detail)).ToArray(),
        a.StablePublicId, Permalink(a.StablePublicId), a.SchemaId, a.SchemaVersion,
        ReadDocument(a.DocumentJson, a.Title, a.Body, $"Post {a.Id} has an invalid document."),
        a.Translations,
        a.Contents.Select(link => ToDto(link, items.GetValueOrDefault(link.ContentId))).ToList(),
        a.Slugs.OrderBy(s => s.Culture).ToDictionary(s => s.Culture, s => s.Slug),
        urlPrefix, a.CanonicalPath(), a.Categories, a.UnitId, a.UnitPath);
}
