using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Create;

/// <summary>
/// Write a new Post (a draft) in a category; the caller becomes its owner. <see cref="PostKind"/> is a published DynamicFields
/// root post field key (an old enum name such as <c>News</c> is still accepted, for one release). <see cref="CategoryId"/> is
/// the primary category; <see cref="CategoryIds"/> more categories of the same press (the author right is needed in each).
/// Without a <see cref="Document"/> the post gets a root of its kind, with the title as its headline.
/// With an <see cref="IdempotencyKey"/>, a repeat by the same caller returns the post made first (the rest of the request
/// is not compared); the same key from another caller is refused.
/// </summary>
public sealed record CreatePostCommand(string CategoryId, string Title, string Body, string PostKind, PostDocument? Document,
    IReadOnlyList<string>? CategoryIds = null, string? IdempotencyKey = null)
    : ICommand<Result<PostDetail>>;
