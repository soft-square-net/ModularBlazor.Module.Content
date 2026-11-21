namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.PublishDue;

/// <summary>
/// Publish every scheduled Post that is due and unchanged since it was scheduled (called by the workflow module; the
/// caller needs the publish right tenant-wide). Returns the ids published.
/// </summary>
public sealed record PublishDuePostsCommand : ICommand<Result<IReadOnlyList<int>>>;
