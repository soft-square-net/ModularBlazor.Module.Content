namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Kinds;

/// <summary>
/// The post kinds a new post can be of: the tenant's published DynamicFields root post fields, read with the caller's
/// token (as the designer's picker reads them), by title. Unavailable when DynamicFields cannot answer.
/// </summary>
public sealed record ListPostKindsQuery : IQuery<Result<IReadOnlyList<PostKindInfo>>>;

public sealed class ListPostKindsHandler(PostKindRules kinds, PostAccess access)
    : IQueryHandler<ListPostKindsQuery, Result<IReadOnlyList<PostKindInfo>>>
{
    public async ValueTask<Result<IReadOnlyList<PostKindInfo>>> Handle(ListPostKindsQuery query, CancellationToken ct)
    {
        _ = access.Me;
        return await kinds.ListAsync(ct);
    }
}
