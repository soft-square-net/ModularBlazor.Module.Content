using System.Text.Json;

namespace soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

/// <summary>One nested content item. Nodes share the root Post's workflow and permanent URL.</summary>
public sealed record PostNode(
    string NodeId,
    PostKind PostKind,
    string SchemaId,
    int SchemaVersion,
    JsonElement Content,
    JsonElement Style,
    IReadOnlyList<PostNode> Children);

/// <summary>
/// Versioned Post form data and composition. Content and Style are independent JSON objects;
/// nodes form one semantic tree shared by all viewports, with responsive values in Style.
/// </summary>
public sealed record PostDocument(
    string SchemaId,
    int SchemaVersion,
    JsonElement Content,
    JsonElement Style,
    IReadOnlyList<PostNode> Children)
{
    public static PostDocument FromLegacy(string title, string body)
    {
        using var content = JsonDocument.Parse(JsonSerializer.Serialize(new { title, body }));
        using var style = JsonDocument.Parse("{}");
        return new PostDocument("legacy.basic", 1, content.RootElement.Clone(), style.RootElement.Clone(), []);
    }
}

internal static class PostJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
