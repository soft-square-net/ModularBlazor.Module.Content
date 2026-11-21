namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Schedule;

public sealed class SchedulePostHandler(PostChanges changes, PostAccess access) : ICommandHandler<SchedulePostCommand, Result<PostDetail>>
{
    public async ValueTask<Result<PostDetail>> Handle(SchedulePostCommand command, CancellationToken ct) =>
        await changes.ChangeAsync(command.Id, command.Version, async post =>
        {
            var publisher = await access.RequirePublisherAsync(post, ct);
            if (!publisher.IsSuccess) return publisher;
            post.Schedule(access.Me, command.PublishAt.ToUniversalTime(), access.Now);
            return Result.Success();
        }, ct);
}
