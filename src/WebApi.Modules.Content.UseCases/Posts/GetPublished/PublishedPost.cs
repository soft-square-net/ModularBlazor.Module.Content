using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.GetPublished;

/// <summary>
/// The live, immutable snapshot of a published Post, served at its permanent URL. <see cref="Languages"/>: the languages
/// it is translated into; <see cref="ServedLanguages"/>: the ones this document's text came from, best first (none: its own).
/// <see cref="PostKind"/> is a DynamicFields root post field key; <see cref="UrlPrefix"/> the kind's URL prefix when it was
/// published, and <see cref="CanonicalPath"/> the post's public path, <c>/{urlPrefix}/{slug}</c>: the slug of the served
/// culture (then the asked one and its parents, then the first), else the public id. Both are null for a post published
/// before kinds had prefixes, until it is published again. <see cref="Body"/> is the published plain-text body: a root of a
/// DynamicFields kind has no value for it, so it is served beside the document.
/// </summary>
public sealed record PublishedPost(int Id, string PublicId, string Permalink, string PostKind, string PressId,
    string CategoryId, string Title, DateTimeOffset PublishedAt, string SchemaId, int SchemaVersion, PostDocument Document,
    IReadOnlyList<string> Languages, IReadOnlyList<string> ServedLanguages, string? UrlPrefix, string? CanonicalPath, string Body = "");
