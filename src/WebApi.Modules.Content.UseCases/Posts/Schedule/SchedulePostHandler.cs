using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Kinds;
using soft.square.net.WebApi.Modules.Content.UseCases.Validation;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Schedule;

/// <summary>
/// Desk schedules the current version: like publishing, the contents waiting for the Desk are accepted first and every
/// required content must be accepted; their revisions are pinned now and published when the time comes. DynamicFields first
/// checks the whole post (<see cref="ContentValidation.CheckPostAsync"/>).
/// </summary>
public sealed class SchedulePostHandler(PostChanges changes, PostAccess access, DeskPublishing desk, PostKindRules kinds,
    ContentValidation validation) : ICommandHandler<SchedulePostCommand, Result<PostDetail>>
{
    public async ValueTask<Result<PostDetail>> Handle(SchedulePostCommand command, CancellationToken ct) =>
        await changes.ChangeAsync(command.Id, command.Version, async post =>
        {
            var publisher = await access.RequirePublisherAsync(post, ct);
            if (!publisher.IsSuccess) return publisher;
            var kind = await kinds.ForPublishingAsync(post, ct); // D3: refused now rather than when it falls due
            if (!kind.IsSuccess) return kind.AsResult();
            if (DeskPublishing.MayPublish(post)) // otherwise the aggregate refuses the state (409) before anything is checked
            {
                var valid = await validation.CheckPostAsync(post, "schedule", ct);
                if (!valid.IsSuccess) return valid;
            }
            var plan = await desk.PlanAsync(post, ct);
            post.Schedule(access.Me, command.PublishAt.ToUniversalTime(), access.Now, plan.Pins);
            return Result.Success();
        }, ct);
}
