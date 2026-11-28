using System.Text.Json;

namespace soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

/// <summary>
/// One nested content item. Nodes share the root Post's workflow and permanent URL. <see cref="PostKind"/> is the kind
/// of the post it was made for, a root field key (old enum names are read as their keys, see <see cref="PostKinds"/>);
/// none means <see cref="PostKinds.Article"/>, as the old enum's default did.
/// </summary>
public sealed record PostNode(
    string NodeId,
    string PostKind,
    string SchemaId,
    int SchemaVersion,
    JsonElement Content,
    JsonElement Style,
    IReadOnlyList<PostNode> Children)
{
    public string PostKind { get; init; } = string.IsNullOrWhiteSpace(PostKind) ? PostKinds.Article : PostKinds.Normalize(PostKind);
}

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
    /// <summary>The schema of a document made from a legacy title and body (not a DynamicFields field).</summary>
    public const string LegacySchemaId = "legacy.basic";

    /// <summary>The root value a post's title goes to, in the kinds that have it.</summary>
    public const string HeadlineKey = "headline";

    /// <summary>
    /// The root of a new post of <paramref name="kind"/> (a DynamicFields root post field, at its published version): its
    /// headline is the title when the field has one (<paramref name="hasHeadline"/>). The other values are left to be filled
    /// in; DynamicFields' checks say which of them the post still needs.
    /// </summary>
    public static PostDocument NewRoot(string kind, int version, bool hasHeadline, string title)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (hasHeadline && !string.IsNullOrWhiteSpace(title)) values[HeadlineKey] = title.Trim();
        return new PostDocument(kind, version, JsonSerializer.SerializeToElement(values), JsonSerializer.SerializeToElement(new { }), []);
    }

    /// <summary>The string value of a root key, or null.</summary>
    public string? RootText(string key) =>
        Content.ValueKind == JsonValueKind.Object && Content.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public static PostDocument FromLegacy(string title, string body)
    {
        using var content = JsonDocument.Parse(JsonSerializer.Serialize(new { title, body }));
        using var style = JsonDocument.Parse("{}");
        return new PostDocument(LegacySchemaId, 1, content.RootElement.Clone(), style.RootElement.Clone(), []);
    }
}

internal static class PostJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
