namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.AutoAssign;

/// <summary>
/// Auto-assign an accepted work to an editor whose category grant is active now. The database version check makes
/// this safe against a manager assigning at the same time; an already assigned work is never overwritten.
/// </summary>
public sealed record AutoAssignPostCommand(int Id) : ICommand<Result<PostDetail>>;
