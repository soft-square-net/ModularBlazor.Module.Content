namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Submit;

public sealed class SubmitPostHandler(PostChanges changes, PostAccess access) : ICommandHandler<SubmitPostCommand, Result<PostDetail>>
{
    public async ValueTask<Result<PostDetail>> Handle(SubmitPostCommand command, CancellationToken ct) =>
        await changes.ChangeAsync(command.Id, command.Version, post =>
        {
            post.Submit(access.Me, access.Now);
            return Task.FromResult(Result.Success());
        }, ct);
}
