using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.ListAcceptedForAutomation;

public sealed class ListAcceptedForAutomationHandler(IReadRepository<Post> reader, PostAccess access)
    : IQueryHandler<ListAcceptedForAutomationQuery, Result<IReadOnlyList<PostSummary>>>
{
    public async ValueTask<Result<IReadOnlyList<PostSummary>>> Handle(ListAcceptedForAutomationQuery query, CancellationToken ct)
    {
        var allowed = await access.RequireTenantWideAsync(ContentPermissions.Assign, "Workflow polling needs tenant-wide content.assign.", ct);
        if (!allowed.IsSuccess) return allowed.As<IReadOnlyList<PostSummary>>();
        return await reader.ListAsync(new AcceptedPostsSpec(), ct);
    }
}
