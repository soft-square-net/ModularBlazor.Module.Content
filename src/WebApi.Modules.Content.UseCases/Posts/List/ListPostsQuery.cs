namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.List;

/// <summary>One of the caller's Post lists (<see cref="PostView"/>).</summary>
public sealed record ListPostsQuery(PostView View) : IQuery<Result<IReadOnlyList<PostSummary>>>;
