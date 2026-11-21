using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Edit;

/// <summary>
/// Edit a Post: its owner (draft, returned, or submitted within the window), the assigned reviewer (in review, on
/// shift), or Desk (approved, scheduled, awaiting re-approval, or published: the live snapshot stays until re-approved).
/// </summary>
public sealed record EditPostCommand(int Id, int Version, string Title, string Body, PostDocument? Document) : ICommand<Result<PostDetail>>;
