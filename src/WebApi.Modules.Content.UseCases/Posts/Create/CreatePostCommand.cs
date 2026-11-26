using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Create;

/// <summary>
/// Write a new Post (a draft) in a category; the caller becomes its owner. <see cref="PostKind"/> is a published DynamicFields
/// root post field key (an old enum name such as <c>News</c> is still accepted, for one release).
/// </summary>
public sealed record CreatePostCommand(string CategoryId, string Title, string Body, string PostKind, PostDocument? Document)
    : ICommand<Result<PostDetail>>;
