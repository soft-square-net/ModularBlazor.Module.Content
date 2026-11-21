namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.GetById;

public sealed class GetPostByIdHandler(PostDetails details) : IQueryHandler<GetPostByIdQuery, Result<PostDetail>>
{
    public async ValueTask<Result<PostDetail>> Handle(GetPostByIdQuery query, CancellationToken ct) =>
        await details.GetAsync(query.Id, ct);
}
