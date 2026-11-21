using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Create;

/// <summary>Write a new Post (a draft) in a category; the caller becomes its owner.</summary>
public sealed record CreatePostCommand(string CategoryId, string Title, string Body, PostKind PostKind, PostDocument? Document)
    : ICommand<Result<PostDetail>>;
