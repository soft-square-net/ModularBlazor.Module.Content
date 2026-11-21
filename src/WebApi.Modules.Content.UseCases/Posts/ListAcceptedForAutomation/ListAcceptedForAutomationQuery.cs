namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.ListAcceptedForAutomation;

/// <summary>Durable polling view for the workflow (Elsa); only a tenant-wide assigner may read it.</summary>
public sealed record ListAcceptedForAutomationQuery : IQuery<Result<IReadOnlyList<PostSummary>>>;
