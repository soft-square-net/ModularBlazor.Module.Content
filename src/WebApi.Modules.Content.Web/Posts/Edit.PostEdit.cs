using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.Web.Posts;

/// <summary>The body of <see cref="Edit"/>. Its name is the OpenAPI schema name: keep it stable.</summary>
public sealed record PostEdit(int Version, string Title, string? Body, PostDocument? Document = null);
