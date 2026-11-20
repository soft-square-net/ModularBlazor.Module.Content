namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.GetById;

/// <summary>One Post with its history, if the caller may see it.</summary>
public sealed record GetPostByIdQuery(int Id) : IQuery<Result<PostDetail>>;
