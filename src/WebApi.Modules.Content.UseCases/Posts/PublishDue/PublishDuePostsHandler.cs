using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate.Specifications;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.PublishDue;

public sealed class PublishDuePostsHandler(IRepository<Post> posts, PostAccess access)
    : ICommandHandler<PublishDuePostsCommand, Result<IReadOnlyList<int>>>
{
    public async ValueTask<Result<IReadOnlyList<int>>> Handle(PublishDuePostsCommand command, CancellationToken ct)
    {
        var allowed = await access.RequireTenantWideAsync(ContentPermissions.Publish,
            "Publishing due posts needs the publish right for the whole tenant.", ct);
        if (!allowed.IsSuccess) return allowed.As<IReadOnlyList<int>>();

        var published = new List<int>();
        foreach (var post in await posts.ListAsync(new DueScheduledSpec(access.Now), ct))
        {
            if (!post.PublishIfDue(access.Me, access.Now)) continue;
            await posts.UpdateAsync(post, ct);
            published.Add(post.Id);
        }
        return published;
    }
}
