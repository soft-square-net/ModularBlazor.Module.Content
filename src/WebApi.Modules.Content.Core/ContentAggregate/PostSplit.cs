using soft.square.net.WebApi.Modules.Content.Core.PipelineAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.Core.ContentAggregate;

/// <summary>
/// The migration of a post stored before contents were shared: each top-level child of its document becomes a content
/// (revision 1, the same node id, made by the post's owner for this post). Nested nodes stay inside their content. The
/// stage follows the post's state, so the workflow carries on where it was:
/// <list type="bullet">
/// <item>scheduled or published: accepted, and pinned to revision 1 (what is or will go live);</item>
/// <item>approved or awaiting re-approval: waiting for the Desk (the review stage), which accepts them when it publishes;</item>
/// <item>any other state: the pipeline's start stage.</item>
/// </list>
/// The post's documents (working and published) do not change.
/// </summary>
public static class PostSplit
{
    public static IReadOnlyList<ContentItem> Migrate(Post post, PostDocument document, PipelineDefinition pipeline, DateTimeOffset now)
    {
        if (post.ContentsSplit) return [];
        var accepted = post.State is PostState.Scheduled or PostState.Published;
        var stage = accepted
            ? pipeline.OfKind(StageKind.Accepted).First().Key
            : post.State is PostState.Approved or PostState.AwaitingDeskReapproval
                ? pipeline.OfKind(StageKind.Review).FirstOrDefault()?.Key ?? pipeline.StartStage
                : pipeline.StartStage;
        var contents = (document.Children ?? []).Select(node => ContentItem.Migrate(post, node, stage, accepted, now)).ToList();
        post.CompleteSplit(contents.Select(c => c.Id).ToList(), pinned: accepted);
        return contents;
    }
}
