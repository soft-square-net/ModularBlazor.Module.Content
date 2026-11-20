namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Return;

public sealed class ReturnPostHandler(PostChanges changes, PostAccess access) : ICommandHandler<ReturnPostCommand, Result<PostDetail>>
{
    public async ValueTask<Result<PostDetail>> Handle(ReturnPostCommand command, CancellationToken ct) =>
        await changes.ChangeAsync(command.Id, command.Version, async post =>
        {
            var allowed = await access.RequireAsync(ContentPermissions.Return, post.Scopes, "send posts back in this category now", ct);
            if (!allowed.IsSuccess) return allowed;
            post.Return(access.Me, command.Reason, access.Now);
            return Result.Success();
        }, ct);
}
