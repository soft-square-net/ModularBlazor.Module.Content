namespace soft.square.net.WebApi.Modules.Content.Web.Posts;

/// <summary>The body of <see cref="Schedule"/>. Its name is the OpenAPI schema name: keep it stable.</summary>
public sealed record ScheduleRequest(int Version, DateTimeOffset PublishAt);
