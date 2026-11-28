namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Assign;

/// <summary>
/// Assign a reviewer. The assignee must hold the review right for this Post right now (an editor on a running shift that
/// covers it, say).
/// </summary>
public sealed record AssignPostCommand(int Id, int Version, string AssigneeId) : ICommand<Result<PostDetail>>;
