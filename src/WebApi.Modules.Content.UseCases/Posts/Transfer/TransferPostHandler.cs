namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Transfer;

public sealed class TransferPostHandler(PostChanges changes, PostAccess access, AssignmentRights rights) : ICommandHandler<TransferPostCommand, Result<PostDetail>>
{
    public async ValueTask<Result<PostDetail>> Handle(TransferPostCommand command, CancellationToken ct) =>
        await changes.ChangeAsync(command.Id, command.Version, async post =>
        {
            var allowed = await rights.RequireAsync(post.Scopes,
                () => access.RequireAsync(ContentPermissions.Transfer, post.Scopes, "hand over work in this category now", ct), ct);
            if (!allowed.IsSuccess) return allowed;
            post.Transfer(access.Me, command.FromShift, access.Now);
            return Result.Success();
        }, ct);
}
