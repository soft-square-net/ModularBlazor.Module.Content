using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;
using soft.square.net.WebApi.Modules.Content.Core.PostAggregate.Specifications;
using soft.square.net.WebApi.Modules.Content.UseCases.Contents;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.PublishDue;

/// <summary>Publishes the due scheduled posts, each with its contents at the revisions pinned when it was scheduled.</summary>
public sealed class PublishDuePostsHandler(IRepository<Post> posts, PostAccess access, PostContents contents)
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
            if (!post.IsDue(access.Now)) continue;
            var document = post.ContentLinks.Count == 0 ? null : await contents.PinnedDocumentAsync(post, ct);
            if (!post.PublishIfDue(access.Me, access.Now, document)) continue;
            await posts.UpdateAsync(post, ct);
            published.Add(post.Id);
        }
        return published;
    }
}
