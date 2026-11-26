namespace soft.square.net.Content.Contracts;

/// <summary>
/// Content's live topic, <c>content:post:{id}</c>: a small nudge (<see cref="ContentLiveMessage"/>) whenever the post's feed
/// (<c>GET /api/content/posts/{id}/events</c>) has a new row; the subscriber then re-reads the feed.
/// </summary>
public static class ContentLiveTopic
{
    public const string Module = "content";
    /// <summary>The topic kind (the manifest's <c>LiveTopic("post", …)</c>).</summary>
    public const string Kind = "post";
    /// <summary>The type of every message on the topic (the payload says which feed row it is).</summary>
    public const string MessageType = "post.changed";

    /// <summary><c>content:post:{postId}</c>.</summary>
    public static string Of(int postId) => $"{Module}:{Kind}:{postId.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
}

/// <summary>The payload of <see cref="ContentLiveTopic.MessageType"/>: which feed row to read (camelCase; null values omitted).</summary>
public sealed record ContentLiveMessage(long EventId, string Type, Guid? ContentId);
