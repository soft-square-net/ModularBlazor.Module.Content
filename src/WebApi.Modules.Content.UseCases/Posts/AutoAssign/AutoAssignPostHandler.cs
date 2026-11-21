using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate.Specifications;
using soft.square.net.WebApi.Modules.Content.UseCases.Posts.Assign;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.AutoAssign;

/// <summary>Picks the editor, then runs the ordinary assignment (<see cref="AssignPostCommand"/>) through the mediator.</summary>
public sealed class AutoAssignPostHandler(IReadRepository<Post> reader, PostAccess access, IGrantStore grants, ISender sender)
    : ICommandHandler<AutoAssignPostCommand, Result<PostDetail>>
{
    public async ValueTask<Result<PostDetail>> Handle(AutoAssignPostCommand command, CancellationToken ct)
    {
        var id = command.Id;
        var post = await reader.FirstOrDefaultAsync(new PostByIdSpec(id), ct);
        if (post is null) return PostDetails.NotFound(id);
        if (post.State != PostState.Accepted)
            return Result<PostDetail>.Conflict("The work is no longer waiting for assignment.");

        var allowed = await access.RequireAsync(ContentPermissions.Assign, post.Scopes, "auto-assign in this category", ct);
        if (!allowed.IsSuccess) return allowed.As<PostDetail>();

        var now = access.Now;
        var candidates = (await grants.ListAsync(ct: ct))
            .Where(g => g.Permission == ContentPermissions.Review && g.IsActiveAt(now)
                        && g.Source.StartsWith("shift:", StringComparison.Ordinal)
                        && g.Covers(post.Scopes) && g.UserId != post.OwnerId)
            .Select(g => (g.UserId, ShiftId: g.Source["shift:".Length..]))
            .Distinct()
            .OrderBy(x => x.UserId, StringComparer.Ordinal)
            .ThenBy(x => x.ShiftId, StringComparer.Ordinal)
            .ToArray();
        if (candidates.Length == 0)
            return Result<PostDetail>.Forbidden("There is no eligible editor on an active shift for this category.");

        var candidate = candidates[(int)((uint)id % (uint)candidates.Length)];
        return await sender.Send(new AssignPostCommand(id, post.Version, candidate.UserId, candidate.ShiftId), ct);
    }
}
