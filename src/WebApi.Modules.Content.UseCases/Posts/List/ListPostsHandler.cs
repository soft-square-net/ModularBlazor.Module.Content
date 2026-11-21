using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.List;

public sealed class ListPostsHandler(IReadRepository<Post> reader, PostAccess access)
    : IQueryHandler<ListPostsQuery, Result<IReadOnlyList<PostSummary>>>
{
    public async ValueTask<Result<IReadOnlyList<PostSummary>>> Handle(ListPostsQuery query, CancellationToken ct)
    {
        var me = access.Me;
        switch (query.View)
        {
            case PostView.Mine:
                return await reader.ListAsync(new PostListSpec(a => a.OwnerId == me), ct);
            case PostView.Review:
                return await reader.ListAsync(new PostListSpec(a => a.AssigneeId == me && a.State == PostState.InReview), ct);
            case PostView.Queue:
            {
                var scope = await access.ScopeFilterAsync([ContentPermissions.Accept, ContentPermissions.Assign], ct);
                return scope is null ? [] : await reader.ListAsync(new PostListSpec(
                    a => a.State == PostState.Submitted || a.State == PostState.Accepted, scope), ct);
            }
            default:
            {
                var scope = await access.ScopeFilterAsync([ContentPermissions.Publish], ct);
                return scope is null ? [] : await reader.ListAsync(new PostListSpec(
                    a => a.State == PostState.Approved || a.State == PostState.Scheduled || a.State == PostState.AwaitingDeskReapproval, scope), ct);
            }
        }
    }
}
