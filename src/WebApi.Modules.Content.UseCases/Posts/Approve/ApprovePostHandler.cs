namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Approve;

public sealed class ApprovePostHandler(PostChanges changes, PostAccess access) : ICommandHandler<ApprovePostCommand, Result<PostDetail>>
{
    public async ValueTask<Result<PostDetail>> Handle(ApprovePostCommand command, CancellationToken ct) =>
        await changes.ChangeAsync(command.Id, command.Version, async post =>
        {
            var reviewer = await access.RequireAsync(ContentPermissions.Review, post.Scopes, "review in this category now (is your shift over?)", ct);
            if (!reviewer.IsSuccess) return reviewer;
            post.Approve(access.Me, access.Now);
            return Result.Success();
        }, ct);
}
