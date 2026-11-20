namespace soft.square.net.WebApi.Modules.Content.Web.Posts;

/// <summary>The body of <see cref="Return"/>. Its name is the OpenAPI schema name: keep it stable.</summary>
public sealed record ReturnRequest(int Version, string? Reason);
