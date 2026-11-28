namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Assign;

public sealed class AssignPostHandler(PostChanges changes, PostAccess access, AssignmentRights rights, IGrantStore grants)
    : ICommandHandler<AssignPostCommand, Result<PostDetail>>
{
    public async ValueTask<Result<PostDetail>> Handle(AssignPostCommand command, CancellationToken ct) =>
        await changes.ChangeAsync(command.Id, command.Version, async post =>
        {
            if (string.IsNullOrWhiteSpace(command.ShiftId))
                return Result.Forbidden("Assignment must name the active shift that grants the editor access.");
            var manager = await rights.RequireAsync(post.Scopes,
                () => access.RequireShiftPermissionAsync(ContentPermissions.Assign, post.Scopes, command.ShiftId, ct), ct);
            if (!manager.IsSuccess) return manager;
            var targetIsEditor = (await grants.ListAsync(command.AssigneeId, $"shift:{command.ShiftId}", ct)).Any(g =>
                g.Permission == ContentPermissions.Review && g.IsActiveAt(access.Now) && g.Covers(post.Scopes));
            if (!targetIsEditor)
                return Result.Conflict($"'{command.AssigneeId}' is not an editor on an active shift covering this post.");
            post.Assign(access.Me, command.AssigneeId, command.ShiftId, access.Now);
            return Result.Success();
        }, ct);
}
