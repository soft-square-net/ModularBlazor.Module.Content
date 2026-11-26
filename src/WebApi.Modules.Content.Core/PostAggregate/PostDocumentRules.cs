using System.Text.Json;

namespace soft.square.net.WebApi.Modules.Content.Core.PostAggregate;

/// <summary>Structural safety checks. Dynamic Fields performs schema-specific validation through its module contract.</summary>
public static class PostDocumentRules
{
    private const int MaxNodes = 500;
    private const int MaxDepth = 12;
    private const int MaxJsonBytes = 2_000_000;

    public static void Validate(PostDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.SchemaId) || document.SchemaId.Length > 128)
            throw new DomainException("A Post document needs a schema id (at most 128 characters).");
        if (document.SchemaVersion < 1) throw new DomainException("A Post schema version must be positive.");
        RequireObject(document.Content, "Content");
        RequireObject(document.Style, "Style");
        var count = 0;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var child in document.Children ?? []) ValidateNode(child, 1, ids, ref count);
        if (JsonSerializer.SerializeToUtf8Bytes(document, PostJson.Options).Length > MaxJsonBytes)
            throw new DomainException("The Post document exceeds the 2 MB limit.");
    }

    /// <summary>One node and what it holds on its own (a shared content): the same limits as inside a document.</summary>
    public static void Validate(PostNode node)
    {
        var count = 0;
        ValidateNode(node, 1, new HashSet<string>(StringComparer.Ordinal), ref count);
        if (JsonSerializer.SerializeToUtf8Bytes(node, PostJson.Options).Length > MaxJsonBytes)
            throw new DomainException("A content exceeds the 2 MB limit.");
    }

    private static void ValidateNode(PostNode node, int depth, HashSet<string> ids, ref int count)
    {
        if (++count > MaxNodes) throw new DomainException($"A Post can contain at most {MaxNodes} nested nodes.");
        if (depth > MaxDepth) throw new DomainException($"Post composition nesting cannot exceed {MaxDepth} levels.");
        if (string.IsNullOrWhiteSpace(node.NodeId) || node.NodeId.Length > 64)
            throw new DomainException("Every Post node needs a node id (at most 64 characters).");
        if (!ids.Add(node.NodeId)) throw new DomainException($"Post node id '{node.NodeId}' is duplicated.");
        if (!PostKinds.IsKey(node.PostKind)) throw new DomainException($"Unsupported postKind '{node.PostKind}': a root field key such as {PostKinds.Article}.");
        if (string.IsNullOrWhiteSpace(node.SchemaId) || node.SchemaId.Length > 128 || node.SchemaVersion < 1)
            throw new DomainException("Every Post node needs a valid schema id and positive version.");
        RequireObject(node.Content, "Node Content");
        RequireObject(node.Style, "Node Style");
        foreach (var child in node.Children ?? []) ValidateNode(child, depth + 1, ids, ref count);
    }

    private static void RequireObject(JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new DomainException($"{name} must be a JSON object.");
    }
}
