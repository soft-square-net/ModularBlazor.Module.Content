using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate.Specifications;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.HandOverShiftWork;

public sealed class HandOverShiftWorkHandler(IRepository<Post> posts, PostAccess access) : ICommandHandler<HandOverShiftWorkCommand, Result<int>>
{
    public async ValueTask<Result<int>> Handle(HandOverShiftWorkCommand command, CancellationToken ct)
    {
        var allowed = await access.RequireTenantWideAsync(ContentPermissions.Transfer, "Automatic handover needs tenant-wide content.transfer.", ct);
        if (!allowed.IsSuccess) return allowed.As<int>();

        var pending = await posts.ListAsync(new AssignedShiftPostsSpec(command.ShiftId), ct);
        foreach (var post in pending)
        {
            post.Transfer(access.Me, command.ShiftId, access.Now);
            await posts.UpdateAsync(post, ct);
        }
        return pending.Count;
    }
}
