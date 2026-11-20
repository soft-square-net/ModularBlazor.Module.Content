namespace soft.square.net.WebApi.Modules.Content.Web.Posts;

/// <summary>The body of <see cref="Transfer"/>. Its name is the OpenAPI schema name: keep it stable.</summary>
public sealed record TransferRequest(int Version, string? FromShift);
