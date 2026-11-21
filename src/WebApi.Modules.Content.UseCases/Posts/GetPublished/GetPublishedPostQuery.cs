namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.GetPublished;

/// <summary>
/// Resolve a permanent Post URL (<c>{publicId}</c>, or <c>legacy-{id}</c>). Anonymous: only the immutable published
/// snapshot is returned, never the current draft. With a culture (ar-EG), its text comes from the published translations
/// of that culture and its parents (ar-EG, then ar), the document's own filling in the rest.
/// </summary>
public sealed record GetPublishedPostQuery(string PublicId, string? Culture = null) : IQuery<Result<PublishedPost>>;
