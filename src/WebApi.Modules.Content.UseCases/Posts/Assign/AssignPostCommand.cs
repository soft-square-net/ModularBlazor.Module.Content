namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Assign;

/// <summary>
/// Assign a reviewer. The assignee must hold the review right for this Post right now (an Editor on an active shift
/// covering it), and the shift must be named.
/// </summary>
public sealed record AssignPostCommand(int Id, int Version, string AssigneeId, string? ShiftId) : ICommand<Result<PostDetail>>;
