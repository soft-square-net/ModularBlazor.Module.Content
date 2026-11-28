namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Accept;

public sealed class AcceptPostHandler(PostChanges changes, PostAccess access, AssignmentRights rights) : ICommandHandler<AcceptPostCommand, Result<PostDetail>>
{
    public async ValueTask<Result<PostDetail>> Handle(AcceptPostCommand command, CancellationToken ct) =>
        await changes.ChangeAsync(command.Id, command.Version, async post =>
        {
            var manager = await rights.RequireAsync(post,
                () => access.RequireAsync(ContentPermissions.Accept, post.Scopes, "accept submissions in this category now", ct), ct);
            if (!manager.IsSuccess) return manager;
            post.Accept(access.Me, command.ShiftId, access.Now);
            return Result.Success();
        }, ct);
}
