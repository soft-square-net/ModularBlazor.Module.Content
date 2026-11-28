using Microsoft.Extensions.Logging;
using soft.square.net.WebApi.Contracts.Messaging;
using soft.square.net.WebApi.Modules.Content.UseCases.Contents;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Newsroom;

/// <summary>
/// What Content reads of NewsRoom's project events (<c>ProjectApproved</c>, <c>ProjectStateChanged</c>). Copied here, never
/// referenced: NewsRoom is optional and no type is shared between modules. <see cref="State"/> is only in the state events.
/// </summary>
public sealed record NewsRoomProjectEvent(Guid ProjectId, int? PostId, string? State = null);

/// <summary>
/// Follows which posts the newsroom workflow is working on (<c>Post.IsWorkflowManaged</c>): a post is, from the first event
/// that names it with a project, until the project is Killed or Published. Runs in the platform's inbox transaction, so it
/// never saves.
/// </summary>
public sealed class NewsRoomProjectEventsHandler(IContentStore store, ILogger<NewsRoomProjectEventsHandler> log) : IIntegrationEventHandler
{
    public const string Approved = "newsroom.project-approved.v1";
    public const string Reapproved = "newsroom.project-reapproved.v1";
    public const string StateChanged = "newsroom.project-state-changed.v1";

    public static IReadOnlyList<string> Types { get; } = [Approved, Reapproved, StateChanged];

    public async Task HandleAsync(IntegrationEnvelope envelope, CancellationToken ct)
    {
        var data = envelope.DataAs<NewsRoomProjectEvent>();
        if (data.PostId is not { } postId || data.ProjectId == Guid.Empty) return; // no draft yet: nothing to follow
        if (await store.TrackedPostAsync(postId, ct) is not { } post)
        {
            log.LogWarning("Event {Id} ({Type}) names post {Post}, which this tenant does not have", envelope.Id, envelope.Type, postId);
            return;
        }
        post.FollowWorkflowProject(data.ProjectId, ended: data.State is "Killed" or "Published");
    }
}
