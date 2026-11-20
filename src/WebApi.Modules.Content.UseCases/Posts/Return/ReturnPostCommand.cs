namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Return;

/// <summary>Send a Post back to its author (shift Manager or Desk); the owner can edit and submit it again.</summary>
public sealed record ReturnPostCommand(int Id, int Version, string? Reason) : ICommand<Result<PostDetail>>;
