using System.Text.Json;
using soft.square.net.Content.Contracts;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using soft.square.net.WebApi.Contracts.Live;
using soft.square.net.WebApi.Modules.Content.Core.FeedAggregate;
using soft.square.net.WebApi.Modules.Content.UseCases.Contents;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Live;

/// <summary>
/// The module's live topic: <c>content:post:{id}</c>. A subscriber gets a small nudge whenever the post's feed
/// (<c>GET /posts/{id}/events</c>) has a new row, then re-reads the feed. Declared in the manifest as
/// <see cref="Topic"/>; the hub checks <see cref="Permission"/> on <c>post:{id}</c> and on the post's press, categories and unit
/// (from <see cref="PostLiveScopeResolver"/>).
/// </summary>
public static class PostLive
{
    public const string ModuleId = ContentLiveTopic.Module;
    public const string Kind = ContentLiveTopic.Kind;
    /// <summary>The type of every message on the topic (the payload says which feed row it is).</summary>
    public const string MessageType = ContentLiveTopic.MessageType;

    /// <summary>
    /// Who may subscribe. Content has no single "read a post" permission (a post is seen by its owner, its reviewer and the
    /// holders of <see cref="PostAccess.SeeingPermissions"/>), and a live topic takes one: the authors', the studio's users.
    /// Others still get the changes by reading the feed (the studio falls back to polling when the hub refuses the topic).
    /// </summary>
    public const string Permission = ContentPermissions.Author;

    public static LiveTopic Topic { get; } = new(Kind, Permission);

    public static string TopicOf(int postId) => LiveTopics.Of(ModuleId, Kind, postId.ToString(System.Globalization.CultureInfo.InvariantCulture));
}


/// <summary>Called once feed rows are committed (the DbContext calls it after the save, or after the save's transaction commits).</summary>
public interface IFeedCommitObserver
{
    /// <summary>Must not throw: the save has already succeeded.</summary>
    Task CommittedAsync(IReadOnlyList<FeedEntry> entries, CancellationToken ct);
}

/// <summary>
/// Publishes <see cref="PostLive.MessageType"/> on <c>content:post:{id}</c> for every committed feed row: to its post, or,
/// for a content's row (no post id), to every post that uses that content (the per-post feed joins them the same way).
/// Best effort: without a live notifier (an older host, a test) nothing is sent, and a failure is logged, never thrown.
/// The sequence is null: feed ids are global per tenant, not contiguous per post, so they would look like gaps.
/// </summary>
public sealed class LivePostPublisher(IContentStore store, IServiceProvider services, ILoggerFactory loggers) : IFeedCommitObserver
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task CommittedAsync(IReadOnlyList<FeedEntry> entries, CancellationToken ct)
    {
        if (entries.Count == 0) return;
        try
        {
            if (services.GetService<ILiveNotifier>() is not { } notifier) return;

            var contentIds = entries.Where(e => e.PostId is null && e.ContentId is not null).Select(e => e.ContentId!.Value).Distinct().ToArray();
            var users = (contentIds.Length == 0 ? [] : await store.PostIdsUsingAsync(contentIds, ct))
                .ToLookup(u => u.ContentId, u => u.PostId);

            foreach (var entry in entries.OrderBy(e => e.Id))
            {
                IEnumerable<int> posts = entry.PostId is { } postId ? [postId] : entry.ContentId is { } contentId ? users[contentId] : [];
                var payload = JsonSerializer.SerializeToElement(new ContentLiveMessage(entry.Id, entry.Type, entry.ContentId), Json);
                foreach (var post in posts.Where(p => p > 0).Distinct())
                {
                    try
                    {
                        await notifier.PublishAsync(PostLive.TopicOf(post), PostLive.MessageType, payload, sequence: null, ct);
                    }
                    catch (Exception e)
                    {
                        Log().LogWarning(e, "Live message for post {PostId} (feed {EventId}) was not sent", post, entry.Id);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Log().LogWarning(e, "Live messages for {Count} feed rows were not sent", entries.Count);
        }
    }

    private ILogger Log() => loggers.CreateLogger("soft.square.net.WebApi.Modules.Content.UseCases.Live");
}

/// <summary>
/// The scopes of a post topic beyond <c>post:{id}</c>: the post's <c>press:</c>, a <c>category:</c> per category, and
/// <c>unit:</c> for its unit and every ancestor (<c>UnitScopes.For(path)</c>), so a grant on the press, a category or a unit
/// above the post lets its holder subscribe. Unknown posts and other kinds: none.
/// </summary>
public sealed class PostLiveScopeResolver(IContentStore store) : ILiveScopeResolver
{
    public async Task<IReadOnlyList<string>> ScopesAsync(string kind, string key, CancellationToken ct)
    {
        if (!string.Equals(kind, PostLive.Kind, StringComparison.OrdinalIgnoreCase)) return [];
        if (!int.TryParse(key, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id) || id <= 0) return [];
        return await store.PostScopesAsync(id, ct) is { } scopes ? [.. scopes] : [];
    }
}
