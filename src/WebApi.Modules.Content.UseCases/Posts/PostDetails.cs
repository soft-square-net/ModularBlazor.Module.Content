using System.Text.Json;
using Microsoft.Extensions.Options;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate.Specifications;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts;

/// <summary>Reads one Post as the caller may see it, and maps it to <see cref="PostDetail"/>.</summary>
public sealed class PostDetails(IReadRepository<Post> reader, PostAccess access, IOptions<ContentOptions> options)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The Post, if the caller owns it, reviews it, or holds a content permission on its scopes; otherwise not found.</summary>
    public async Task<Result<PostDetail>> GetAsync(int id, CancellationToken ct)
    {
        var post = await reader.FirstOrDefaultAsync(new PostDetailSpec(id), ct);
        if (post is null) return NotFound(id);
        if (post.OwnerId != access.Me && post.AssigneeId != access.Me && !await access.SeesAsync(post.Scopes, ct)) return NotFound(id);
        return ToDetail(post);
    }

    public static Result<PostDetail> NotFound(int id) => Result<PostDetail>.NotFound($"Post {id} was not found.");

    public static string Permalink(string publicId) => $"/api/content/public/{Uri.EscapeDataString(publicId)}";

    /// <summary>A stored document, or the legacy title and body as one when none was stored.</summary>
    public static PostDocument ReadDocument(string? json, string title, string body, string invalidMessage) =>
        string.IsNullOrWhiteSpace(json) || json == "{}"
            ? PostDocument.FromLegacy(title, body)
            : JsonSerializer.Deserialize<PostDocument>(json, Json) ?? throw new InvalidOperationException(invalidMessage);

    private PostDetail ToDetail(Post a) => new(a.Id, a.PostKind, a.PressId, a.CategoryId, a.Title, a.Body, a.OwnerId, a.State, a.Version,
        a.CreatedAt, a.UpdatedAt, a.SubmittedAt, a.OwnerMayEdit(access.Now, options.Value.OwnerEditWindow), a.AcceptedBy,
        a.AcceptedAt, a.AcceptedShiftId, a.AssigneeId,
        a.AssignedShiftId, a.ReviewerId, a.ScheduledFor, a.ScheduledBy, a.PublishedAt, a.PublishedBy,
        a.PublishedTitle, a.PublishedBody, a.TransferredFromShift,
        a.History.OrderBy(e => e.At).ThenBy(e => e.Id).Select(e => new PostEventDto(e.At, e.Actor, e.Action, e.State, e.Detail)).ToArray(),
        a.StablePublicId, Permalink(a.StablePublicId), a.SchemaId, a.SchemaVersion,
        ReadDocument(a.DocumentJson, a.Title, a.Body, $"Post {a.Id} has an invalid document."));
}
