namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Publish;

/// <summary>Desk publishes the current version now.</summary>
public sealed record PublishPostCommand(int Id, int Version) : ICommand<Result<PostDetail>>;
