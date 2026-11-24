namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Schedule;

/// <summary>
/// Desk schedules the current version: like publishing, the contents waiting for the Desk are accepted first and every
/// required content must be accepted; their revisions are pinned now and published when the time comes.
/// </summary>
public sealed class SchedulePostHandler(PostChanges changes, PostAccess access, DeskPublishing desk) : ICommandHandler<SchedulePostCommand, Result<PostDetail>>
{
    public async ValueTask<Result<PostDetail>> Handle(SchedulePostCommand command, CancellationToken ct) =>
        await changes.ChangeAsync(command.Id, command.Version, async post =>
        {
            var publisher = await access.RequirePublisherAsync(post, ct);
            if (!publisher.IsSuccess) return publisher;
            var plan = await desk.PlanAsync(post, ct);
            post.Schedule(access.Me, command.PublishAt.ToUniversalTime(), access.Now, plan.Pins);
            return Result.Success();
        }, ct);
}
