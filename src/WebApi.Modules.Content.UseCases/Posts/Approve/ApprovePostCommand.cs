namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Approve;

/// <summary>The assigned reviewer approves the Post, while their shift grant is active.</summary>
public sealed record ApprovePostCommand(int Id, int Version) : ICommand<Result<PostDetail>>;
