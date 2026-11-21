using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.UseCases.Posts.GetPublished;

/// <summary>The live, immutable snapshot of a published Post, served at its permanent URL.</summary>
public sealed record PublishedPost(int Id, string PublicId, string Permalink, PostKind PostKind, string PressId,
    string CategoryId, string Title, DateTimeOffset PublishedAt, string SchemaId, int SchemaVersion, PostDocument Document);
