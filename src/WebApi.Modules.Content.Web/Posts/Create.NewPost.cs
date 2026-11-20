using soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

namespace soft.square.net.WebApi.Modules.Content.Web.Posts;

/// <summary>The body of <see cref="Create"/>. Its name is the OpenAPI schema name: keep it stable.</summary>
public sealed record NewPost(string CategoryId, string Title, string? Body, PostKind PostKind = PostKind.Article, PostDocument? Document = null);
