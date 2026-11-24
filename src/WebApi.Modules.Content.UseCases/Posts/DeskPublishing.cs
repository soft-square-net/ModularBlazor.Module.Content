using soft.square.net.WebApi.Modules.Content.Core.PipelineAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.UseCases.Contents;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts;

/// <summary>
/// What Desk's publish (or schedule) of a post does to its contents: those waiting in a review stage are accepted by this
/// Desk user (the pipeline's pinning action, when the user may take it on that content), then the plan pins every content
/// that has an accepted revision. The post refuses the plan when a required content has none (a rejected one, say).
/// </summary>
public sealed class DeskPublishing(PostContents contents, ContentPipelines pipelines, ContentAccess access)
{
    public async Task<PublishPlan> PlanAsync(Post post, CancellationToken ct)
    {
        var items = await contents.LoadAsync(post, ct);
        var pipeline = await pipelines.CurrentAsync(ct);
        foreach (var content in items.Values.Where(c => !c.IsArchived))
            if (ContentPipelines.StageOf(pipeline, content).Kind == StageKind.Review && pipeline.AcceptFrom(content.Stage) is { } accept
                && await access.MatchesAsync(content, accept.Who, ct))
                content.Act(access.Me, accept, null, access.Now);
        return await contents.PlanAsync(post, items, ct);
    }
}
