namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Unschedule;

/// <summary>Take a scheduled Post off the schedule (the Desk, or the workflow when its project is killed).</summary>
public sealed record UnschedulePostCommand(int Id, int Version) : ICommand<Result<PostDetail>>;

public sealed class UnschedulePostHandler(PostChanges changes, PostAccess access) : ICommandHandler<UnschedulePostCommand, Result<PostDetail>>
{
    public async ValueTask<Result<PostDetail>> Handle(UnschedulePostCommand command, CancellationToken ct) =>
        await changes.ChangeAsync(command.Id, command.Version, async post =>
        {
            // Stopping a publication is not publishing: no second pair of eyes is needed (RequirePublisherAsync).
            var desk = await access.RequireAsync(ContentPermissions.Publish, post.Scopes, "unschedule in this category now", ct);
            if (!desk.IsSuccess) return desk;
            post.Unschedule(access.Me, access.Now);
            return Result.Success();
        }, ct);
}
