namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.GetPublished;

/// <summary>
/// Resolve a permanent Post URL (<c>{publicId}</c>, or <c>legacy-{id}</c>). Anonymous: only the immutable published
/// snapshot is returned, never the current draft.
/// </summary>
public sealed record GetPublishedPostQuery(string PublicId) : IQuery<Result<PublishedPost>>;
