namespace soft.square.net.WebApi.Modules.Content.Web.Posts;

/// <summary>
/// The body of the workflow steps that need nothing but the version the caller saw (<see cref="Submit"/>,
/// <see cref="Approve"/>, <see cref="Publish"/>). Its name is the OpenAPI schema name: keep it stable.
/// </summary>
public sealed record VersionRequest(int Version);
