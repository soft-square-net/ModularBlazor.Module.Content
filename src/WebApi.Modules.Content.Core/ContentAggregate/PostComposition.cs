using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;

/// <summary>
/// How a post's document is put together from its root values and its contents (pure functions; the application layer
/// loads what they need). The node ids of the contents must be unique within the post: they key its translations.
/// </summary>
public static class PostComposition
{
    /// <summary>The working document: the root, then each content's current values, in order.</summary>
    public static PostDocument Working(PostDocument root, IEnumerable<Guid> order, IReadOnlyDictionary<Guid, ContentItem> contents) =>
        Assemble(root, order.Select(id => contents.TryGetValue(id, out var content)
            ? content.Node
            : throw new DomainException($"Content {id} was not found.")));

    /// <summary>
    /// What a publication pins: every content that has an accepted revision, at that revision. The post checks that its
    /// required contents are all there.
    /// </summary>
    public static IReadOnlyDictionary<Guid, int> AcceptedPins(IEnumerable<PostContent> refs, IReadOnlyDictionary<Guid, ContentItem> contents) =>
        refs.Where(r => contents.TryGetValue(r.ContentId, out var c) && c.AcceptedRevision is not null)
            .ToDictionary(r => r.ContentId, r => contents[r.ContentId].AcceptedRevision!.Value);

    /// <summary>The published document: the root, then each pinned content at its pinned revision, in order (unpinned ones left out).</summary>
    public static PostDocument Published(PostDocument root, IEnumerable<PostContent> refs, IReadOnlyDictionary<Guid, int> pins,
        Func<Guid, int, PostNode> revision) =>
        Assemble(root, refs.OrderBy(r => r.Order).Where(r => pins.ContainsKey(r.ContentId)).Select(r => revision(r.ContentId, pins[r.ContentId])));

    /// <summary>Requires that the contents' node ids are unique within one post.</summary>
    public static void RequireUniqueNodes(IEnumerable<ContentItem> contents)
    {
        var duplicate = contents.GroupBy(c => c.NodeId, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null) throw new DomainException($"Two contents of the Post have the node id '{duplicate.Key}'.");
    }

    private static PostDocument Assemble(PostDocument root, IEnumerable<PostNode> nodes) => root with { Children = nodes.ToList() };
}
