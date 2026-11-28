namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Assign;

public sealed class AssignPostHandler(PostChanges changes, PostAccess access, AssignmentRights rights)
    : ICommandHandler<AssignPostCommand, Result<PostDetail>>
{
    public async ValueTask<Result<PostDetail>> Handle(AssignPostCommand command, CancellationToken ct) =>
        await changes.ChangeAsync(command.Id, command.Version, async post =>
        {
            var manager = await rights.RequireAsync(post,
                () => access.RequireAsync(ContentPermissions.Assign, post.Scopes, "assign reviewers in this category now", ct), ct);
            if (!manager.IsSuccess) return manager;
            if (string.IsNullOrWhiteSpace(command.AssigneeId)) return Result.Invalid(new ValidationError(nameof(command.AssigneeId), "Name a reviewer."));
            if (!await access.UserHasAsync(command.AssigneeId, ContentPermissions.Review, post.Scopes, ct))
                return Result.Conflict($"'{command.AssigneeId}' does not hold the review right for this post now (is their shift running?).");
            post.Assign(access.Me, command.AssigneeId, access.Now);
            return Result.Success();
        }, ct);
}
