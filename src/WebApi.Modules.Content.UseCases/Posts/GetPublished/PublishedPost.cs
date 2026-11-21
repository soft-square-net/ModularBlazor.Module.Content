using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.GetPublished;

/// <summary>
/// The live, immutable snapshot of a published Post, served at its permanent URL. <see cref="Languages"/>: the languages
/// it is translated into; <see cref="ServedLanguages"/>: the ones this document's text came from, best first (none: its own).
/// </summary>
public sealed record PublishedPost(int Id, string PublicId, string Permalink, PostKind PostKind, string PressId,
    string CategoryId, string Title, DateTimeOffset PublishedAt, string SchemaId, int SchemaVersion, PostDocument Document,
    IReadOnlyList<string> Languages, IReadOnlyList<string> ServedLanguages);
