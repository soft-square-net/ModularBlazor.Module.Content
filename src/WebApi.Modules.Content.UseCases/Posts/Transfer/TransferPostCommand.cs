namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.Transfer;

/// <summary>Handover: unfinished review work goes back to the queue for the next shift's manager.</summary>
public sealed record TransferPostCommand(int Id, int Version, string? FromShift) : ICommand<Result<PostDetail>>;
