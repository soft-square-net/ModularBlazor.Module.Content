namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Submit;

/// <summary>The owner submits a draft or returned Post for review.</summary>
public sealed record SubmitPostCommand(int Id, int Version) : ICommand<Result<PostDetail>>;
