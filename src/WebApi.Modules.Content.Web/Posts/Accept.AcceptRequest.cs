namespace soft.square.net.WebApi.Modules.Content.Web.Posts;

/// <summary>The body of <see cref="Accept"/>. Its name is the OpenAPI schema name: keep it stable.</summary>
public sealed record AcceptRequest(int Version, string? ShiftId = null);
